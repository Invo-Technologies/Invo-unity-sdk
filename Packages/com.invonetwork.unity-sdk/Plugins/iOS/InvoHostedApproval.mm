// InvoSDK - hosted approval bridge for iOS.
//
// Opens INVO's hosted approval page in an ASWebAuthenticationSession: a top-level,
// system-owned browser sheet. WebAuthn (the passkey ceremony) runs there; it does NOT run
// in an embedded WKWebView, which is why this file exists instead of a WebView.
//
// The session ends when the page navigates to invo-sdk-<game_id>://done. That URL carries
// nothing; we report only "done" or "cancelled" to Unity and the game polls its server.
//
// Requires AuthenticationServices.framework (linked by InvoHostedApprovalBuildPostprocessor).

#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <AuthenticationServices/AuthenticationServices.h>

extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg);
extern "C" UIViewController* UnityGetGLViewController();

API_AVAILABLE(ios(13.0))
@interface InvoHostedApprovalSessionHolder : NSObject <ASWebAuthenticationPresentationContextProviding>
@property (nonatomic, strong) ASWebAuthenticationSession *session;
@end

@implementation InvoHostedApprovalSessionHolder
- (ASPresentationAnchor)presentationAnchorForWebAuthenticationSession:(ASWebAuthenticationSession *)session
{
    UIWindow *window = UnityGetGLViewController().view.window;
    if (window == nil) {
        window = [UIApplication sharedApplication].keyWindow;
    }
    return window;
}
@end

static id gInvoHostedApprovalHolder = nil;

extern "C" {

// Returns 1 when the session started, 0 when Unity should fall back to Application.OpenURL.
int _InvoHostedApproval_OpenAuthSession(const char* url, const char* scheme, const char* gameObjectName)
{
    if (url == NULL || scheme == NULL || gameObjectName == NULL) {
        return 0;
    }
    if (@available(iOS 13.0, *)) {
        NSURL *nsUrl = [NSURL URLWithString:[NSString stringWithUTF8String:url]];
        NSString *nsScheme = [NSString stringWithUTF8String:scheme];
        NSString *target = [NSString stringWithUTF8String:gameObjectName];
        if (nsUrl == nil || ![[nsUrl.scheme lowercaseString] isEqualToString:@"https"]) {
            return 0;
        }

        InvoHostedApprovalSessionHolder *previous = gInvoHostedApprovalHolder;
        if (previous != nil && previous.session != nil) {
            [previous.session cancel];
        }

        InvoHostedApprovalSessionHolder *holder = [InvoHostedApprovalSessionHolder new];
        ASWebAuthenticationSession *session = [[ASWebAuthenticationSession alloc]
            initWithURL:nsUrl
            callbackURLScheme:nsScheme
            completionHandler:^(NSURL * _Nullable callbackURL, NSError * _Nullable error) {
                // Cancelling the previous session (above) runs ITS completion block; only the
                // session that is still current may clear the global or report to Unity.
                if (gInvoHostedApprovalHolder != holder) {
                    holder.session = nil;
                    return;
                }
                // The callback URL is deliberately not read: it carries nothing.
                const char* result = (error == nil) ? "done" : "cancelled";
                gInvoHostedApprovalHolder = nil;
                holder.session = nil;
                UnitySendMessage([target UTF8String], "OnNativeSessionFinished", result);
            }];
        session.presentationContextProvider = holder;
        // No cookies are needed by the hosted page (bearer travels in the URL, then in POST
        // bodies), and an ephemeral session skips the "wants to use invo.network to sign in"
        // consent sheet, so the player sees the page directly.
        session.prefersEphemeralWebBrowserSession = YES;
        holder.session = session;
        gInvoHostedApprovalHolder = holder;

        if (![session start]) {
            gInvoHostedApprovalHolder = nil;
            return 0;
        }
        return 1;
    }
    return 0;
}

void _InvoHostedApproval_CancelAuthSession(void)
{
    if (@available(iOS 13.0, *)) {
        InvoHostedApprovalSessionHolder *holder = gInvoHostedApprovalHolder;
        if (holder != nil && holder.session != nil) {
            [holder.session cancel];
        }
        gInvoHostedApprovalHolder = nil;
    }
}

}
