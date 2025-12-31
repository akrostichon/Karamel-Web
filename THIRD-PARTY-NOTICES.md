# Third-Party Notices for Karamel-Web

This file lists third-party libraries used by Karamel-Web, their licenses, and relevant notes.

## Dependencies

- CDGraphics.js
  - Version: referenced via CDN (cdgraphics.js v7.x)
  - License: ISC
  - Source: https://github.com/adrienjoly/cdgraphics
  - Notes: Used client-side for CDG rendering. ISC is permissive and compatible with MIT.

- jsmediatags
  - Version: referenced via CDN (jsmediatags 3.9.5+esm)
  - License: LGPL-3.0
  - Source: https://github.com/aadsm/jsmediatags
  - Notes: LGPL-3.0 is a copyleft license; usage here is client-side only. If you redistribute modified versions of the library, ensure compliance with LGPL-3.0 terms. Consider replacing with an MIT-licensed alternative if redistribution concerns arise.

- QRCode.js
  - Version: referenced via CDN (qrcodejs 1.0.0)
  - License: MIT
  - Source: https://github.com/davidshimjs/qrcodejs
  - Notes: Used for generating QR codes linking to singer pages.

- Fluxor
  - Version: added as a NuGet dependency for Blazor state management
  - License: MIT
  - Source: https://github.com/mrpmorris/Fluxor

## License compatibility

Karamel-Web is licensed under the MIT License. The included third-party libraries listed above use permissive licenses (ISC, MIT) except for jsmediatags which is LGPL-3.0. The project does not statically link or embed the jsmediatags library into server-side binaries; it is used client-side via CDN import which reduces some distribution obligations — but you should still comply with LGPL terms if you redistribute a packaged version including the library.

## How to comply

- Keep this file and the included license files with any redistribution of the project.
- If you modify or bundle LGPL-3.0 code (jsmediatags), provide appropriate source or object files as required by LGPL.
- Provide attribution and license links in documentation and an About page (see Pages/About.razor).

## Contact

If you have questions about licensing or need a different licensing arrangement, contact the project owner listed in the LICENSE file.
