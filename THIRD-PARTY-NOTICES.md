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

## Additional Dependencies Found in Repository

- Bootstrap
  - Version: bundled in repo under `wwwroot/lib/bootstrap/` (dist files)
  - License: MIT
  - Source: https://github.com/twbs/bootstrap
  - Notes: CSS and JS are included in the repository; include Bootstrap's LICENSE when redistributing.

- Bootstrap Icons
  - Version: linked via CDN in `wwwroot/index.html` (bootstrap-icons@1.11.3)
  - License: MIT
  - Source: https://github.com/twbs/icons
  - Notes: Referenced via CDN; if you include icon files in a distribution, include the license text.

- Vitest
  - Version: devDependency in `wwwroot/package.json` (^1.0.0)
  - License: MIT
  - Source: https://github.com/vitest-dev/vitest
  - Notes: Development/test tool; include attribution in developer-facing docs.

- @vitest/ui
  - Version: devDependency in `wwwroot/package.json` (^1.0.0)
  - License: MIT
  - Source: https://github.com/vitest-dev/ui

- happy-dom
  - Version: devDependency in `wwwroot/package.json` (^12.10.3)
  - License: MIT (verify upstream)
  - Source: https://github.com/capricorn86/happy-dom
  - Notes: Test environment for Vitest in Node; development-only dependency.

## Redistribution notes

- For all MIT/ISC-licensed components (Bootstrap, Bootstrap Icons, QRCode.js, CDGraphics.js, Fluxor, Vitest, etc.), include their license text or a link to the source in distributed artifacts.
- For LGPL-3.0 (`jsmediatags`), follow the guidance in the previous section: if you bundle the library in a distributable artifact, provide the source or clear instructions on how to obtain it and preserve the license notices.


## License compatibility

Karamel-Web is licensed under the MIT License. The included third-party libraries listed above use permissive licenses (ISC, MIT) except for jsmediatags which is LGPL-3.0. The project does not statically link or embed the jsmediatags library into server-side binaries; it is used client-side via CDN import which reduces some distribution obligations — but you should still comply with LGPL terms if you redistribute a packaged version including the library.

## How to comply

- Keep this file and the included license files with any redistribution of the project.
- If you modify or bundle LGPL-3.0 code (jsmediatags), provide appropriate source or object files as required by LGPL.
- Provide attribution and license links in documentation and an About page (see Pages/About.razor).

## Contact

If you have questions about licensing or need a different licensing arrangement, contact the project owner listed in the LICENSE file.
