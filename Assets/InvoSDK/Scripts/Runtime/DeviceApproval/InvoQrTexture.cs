using UnityEngine;

namespace InvoSDK
{
    /// <summary>Turns an <see cref="InvoQrCode"/> into a point-filtered texture for a RawImage or IMGUI.</summary>
    public static class InvoQrTexture
    {
        /// <summary>The quiet zone the standard requires on every side, in modules.</summary>
        public const int QuietZoneModules = 4;

        /// <summary>
        /// One texel per module plus the quiet zone. Point filtering keeps the edges hard at any
        /// on-screen size, so scale it up with the UI rather than baking pixels in here.
        /// The caller owns the texture: <c>Object.Destroy</c> it when the QR is taken down.
        /// </summary>
        public static Texture2D Create(InvoQrCode qr)
        {
            int size = qr.Size + QuietZoneModules * 2;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "InvoDeviceApprovalQr"
            };

            var pixels = new Color32[size * size];
            var dark = new Color32(0, 0, 0, 255);
            var light = new Color32(255, 255, 255, 255);
            for (int y = 0; y < size; y++)
            {
                // Texture rows run bottom-up; QR rows run top-down.
                int qrY = size - 1 - y - QuietZoneModules;
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = qr.IsDark(x - QuietZoneModules, qrY) ? dark : light;
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>Encodes <paramref name="text"/> (medium error correction) straight to a texture.</summary>
        public static Texture2D Create(string text)
        {
            return Create(InvoQrCode.EncodeText(text, InvoQrEcc.Medium));
        }
    }
}
