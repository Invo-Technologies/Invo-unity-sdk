# Third-Party Notices

The Invo SDK includes the following third-party components. Everything else in this package is
Copyright (c) 2026 Invo Technologies.

## Inter font

- Files: `Runtime/Art/Inter/Inter-VariableFont_slnt,wght.ttf` and the TextMesh Pro font asset made from it
- Copyright 2020 The Inter Project Authors (https://github.com/rsms/inter)
- License: SIL Open Font License, Version 1.1. The full license text ships next to the font in
  `Runtime/Art/Inter/OFL.txt` and is available at https://openfontlicense.org

## QR Code generator (Project Nayuki)

- Files: `Runtime/Core/DeviceApproval/InvoQrCode.cs` follows the structure and algorithms of
  Project Nayuki's QR Code generator library (https://www.nayuki.io/page/qr-code-generator-library)
- License: MIT

```
Copyright (c) Project Nayuki. (MIT License)
https://www.nayuki.io/page/qr-code-generator-library

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:
- The above copyright notice and this permission notice shall be included in
  all copies or substantial portions of the Software.
- The Software is provided "as is", without warranty of any kind, express or
  implied, including but not limited to the warranties of merchantability,
  fitness for a particular purpose and noninfringement. In no event shall the
  authors or copyright holders be liable for any claim, damages or other
  liability, whether in an action of contract, tort or otherwise, arising from,
  out of or in connection with the Software or the use or other dealings in the
  Software.
```

## Unity packages

The SDK depends on, but does not include, these Unity packages. They are installed by the Package
Manager under their own licenses: `com.unity.nuget.newtonsoft-json` (Newtonsoft Json.NET, MIT),
`com.unity.ugui` and, on Unity 2021.3 and 2022.3, `com.unity.textmeshpro`.
