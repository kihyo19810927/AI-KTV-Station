# Third-party notices

AI-KTV Station uses the following direct runtime dependencies. Exact versions and transitive dependencies remain recorded in the committed NuGet and NPM lock files; the full review is in `docs/project/DEPENDENCY-LICENSE-REVIEW.md`.

## ToolGood.Words 3.1.0.3

- Purpose: Chinese simplified/traditional conversion, pinyin and pinyin initials for precomputed search keys.
- Source: <https://github.com/toolgood/ToolGood.Words>
- Package: <https://www.nuget.org/packages/ToolGood.Words/3.1.0.3>
- License: Apache License 2.0 (<https://www.apache.org/licenses/LICENSE-2.0>)
- Package metadata commit: `43adc0eeb8cb8df647ce6433c8814980add2317b`

The restored NuGet package includes its `LICENSE` file. Release packaging must retain the applicable license and this notice.

## QRCoder 1.8.0

- Purpose: host-side PNG room QR rendering.
- Source: <https://github.com/codebude/QRCoder>
- License: MIT.

## Web runtime

- React / React DOM 19.2.8: MIT.
- React Router 7.18.3: MIT.
- Microsoft SignalR JavaScript client 10.0.11: MIT.
- Lucide React 1.43.0: ISC.

Release packaging must generate and retain the complete transitive third-party notice from the locked dependency graph.

## External media tools

mpv and FFmpeg are external processes and are not licensed as part of Station. The V1 Station package must not bundle either binary. The user installs them separately; see the dependency review for verified local build information and the distribution gate.
