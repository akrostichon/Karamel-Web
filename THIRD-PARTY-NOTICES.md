# Third-Party Notices for Karamel-Web

This file lists third-party libraries used by Karamel-Web, their licenses, and relevant notes.

## Dependencies

 - CDGraphics.js
  - Version: included locally under `wwwroot/lib/cdgraphics` (cdgraphics v7.0.0)
  - License: MIT
  - Source: https://github.com/bhj/cdgraphics
  - Notes: Used client-side for CDG rendering. MIT is permissive and compatible with this project.

- jsmediatags
 - jsmediatags
  - Version: included locally under `wwwroot/lib/jsmediatags` (jsmediatags v3.9.7 as listed in `wwwroot/package.json`)
  - License: LGPL-3.0
  - Source: https://github.com/aadsm/jsmediatags
  - Notes: LGPL-3.0 is a copyleft license; the repository includes a local copy for client-side use. If you redistribute a packaged artifact that bundles this library, ensure compliance with LGPL-3.0 (provide source or follow LGPL requirements). Consider an MIT alternative if redistribution constraints are undesirable.

- QRCode.js
 - QRCode.js
  - Version: included locally under `wwwroot/lib/qrcodejs` (qrcodejs v1.0.0)
  - License: MIT
  - Source: https://github.com/davidshimjs/qrcodejs
  - Notes: Used for generating QR codes linking to singer pages.

- JSZip
 - JSZip
  - Version: included locally under `wwwroot/lib/jszip` (jszip v3.10.1, devDependency in `wwwroot/package.json`)
  - License: MIT
  - Source: https://github.com/Stuk/jszip
  - Notes: Used client-side to enumerate and extract entries from ZIP archives lazily. The project contains a local copy (`wwwroot/lib/jszip/jszip.min.js`); it is not loaded from CDN at runtime. Include the MIT license text when redistributing the package.

- Fluxor
  - Version: added as a NuGet dependency for Blazor state management
  - License: MIT
  - Source: https://github.com/mrpmorris/Fluxor

- @microsoft/signalr
  - Version: included locally under `wwwroot/lib/signalr` and listed in `wwwroot/package.json` (signalr / @microsoft/signalr v10.0.0)
  - License: MIT
  - Source: https://github.com/dotnet/aspnetcore/tree/main/src/SignalR/clients/ts/signalr
  - Notes: Used for client-side SignalR communication with the backend; local copy lives in `wwwroot/lib/signalr`.

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
  - Version: devDependency in `wwwroot/package.json` (^4.0.16)
  - License: MIT
  - Source: https://github.com/vitest-dev/vitest
  - Notes: Development/test tool; include attribution in developer-facing docs.

- @vitest/ui
  - Version: devDependency in `wwwroot/package.json` (^4.0.16)
  - License: MIT
  - Source: https://github.com/vitest-dev/ui

- happy-dom
  - Version: devDependency in `wwwroot/package.json` (^20.0.11)
  - License: MIT (verify upstream)
  - Source: https://github.com/capricorn86/happy-dom
  - Notes: Test environment for Vitest in Node; development-only dependency.

- Awesome GitHub Copilot Instructions
  - Version: adapted from github/awesome-copilot repository
  - License: MIT
  - Source: https://github.com/github/awesome-copilot
  - Notes: Several instruction files in `.github/instructions/` directory were adapted from the Awesome GitHub Copilot community collection (code-review-generic.instructions.md, agents.instructions.md, agent-skills.instructions.md, instructions.instructions.md). These are development-time configuration files for GitHub Copilot customization.

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
