# Rotated selection chrome edge clamp

## Plan
- [x] Keep rotated visual bounds inside the label with enough padding for resize handles.
- [x] Clamp the selected element after rotation changes and before resize drag snapshots.
- [x] Add a regression probe for a rotated text element near the label edge.
- [x] Verify with build/probe and relaunch.

## Notes
- User report: after the bounded text preview fix, rotating the blue box makes the handles appear clipped at the label edge and the box becomes unusable.
- Root cause being addressed: element content can rotate into a visual AABB that extends outside the clipped label canvas; handles are then partially outside the hit-testable/visible area.

## Review
- Added `DesignerInteractionHelper.ClampElementToTemplate(...)` to keep rotated visual bounds inside the label with `SelectionChromePadding` for handles.
- Clamp now runs after rotation changes, when selecting an element, and before resize drag snapshots.
- Added a regression probe for rotated text near the label edge so top/bottom handles remain inside the label when there is enough physical room.
- `dotnet build testbartender.sln --no-restore -v:minimal` passed outside the sandbox with 0 errors.
- `dotnet run --project test_parse.csproj --no-restore` passed outside the sandbox with `Template parity probe passed.`
- Relaunched the rebuilt debug app from `BarTenderClone\bin\Debug\net9.0-windows\BizPro6Barcode.exe`.
- Existing warnings remain: unreachable NuGet vulnerability metadata source `https://store.iotech.mn/v3/index.json` plus pre-existing nullable warnings.

---

# Rotated text bounded preview fix

## Plan
- [x] Replace designer text preview with an explicit bounded shrink-only visual.
- [x] Keep 90/270 text no-wrap inside that bounded visual so long price/code text cannot clip.
- [x] Keep wrapped horizontal text constrained to the visible content width.
- [x] Add a regression probe for screenshot-sized rotated `MNT 395,000`.
- [x] Verify with build/probe and relaunch.

## Notes
- User screenshot still shows `MNT 395,000` clipped as `T 395,` in the rotated small selection box.
- The content becomes full only after making the blue box much larger, so the persisted content and rotation are correct; the designer preview visual is not enforcing the fitted layout inside the current selected box.

## Review
- Replaced the active designer text preview from a self-measuring `TextBlock` to a bounded `Viewbox` with `Stretch=Uniform` and `StretchDirection=DownOnly`.
- Rotated 90/270 text remains no-wrap; long price/code text now shrinks as a full run inside the current selected box instead of being clipped by TextBlock layout.
- Horizontal/wrapped text keeps an explicit layout width so normal wrap behavior is still constrained to the visible content area.
- Added a screenshot-style regression case for `MNT 395,000` rotated 90 degrees inside a small explicit text box.
- `dotnet build testbartender.sln --no-restore -v:minimal` passed outside the sandbox with 0 errors.
- `dotnet run --project test_parse.csproj --no-restore` passed outside the sandbox with `Template parity probe passed.`
- Relaunched the rebuilt debug app from `BarTenderClone\bin\Debug\net9.0-windows\BizPro6Barcode.exe`.
- Existing warnings remain: unreachable NuGet vulnerability metadata source `https://store.iotech.mn/v3/index.json` plus pre-existing nullable warnings.

---

# Rotated text no-wrap fit follow-up

## Plan
- [x] Make the fitted text measurement rotation-aware.
- [x] Force 90/270 designer text through no-wrap fit so price/code strings shrink instead of wrapping/clipping.
- [x] Use the same no-wrap decision in raster print rendering.
- [x] Add a regression probe for rotated narrow text requiring unwrapped width fit.
- [x] Verify with solution build and `test_parse.csproj`, then record results.

## Notes
- User report: rotated text still clips, and only dragging the visible left-middle handle changes whether the content is clipped or fully visible.
- Root cause: the current fit path still allows WPF wrapping. For 90/270 text this can mark the text as "fit" even though the rotated visual needs the original string to fit as one run inside the blue box.

## Review
- `DesignerInteractionHelper.MeasureTextLayout(...)` is now rotation-aware and returns whether the text should wrap.
- 90/270 degree text uses no-wrap fitting, so price/code strings shrink until the full unrotated run fits inside the current local box before rotation.
- Designer `TextBlock.TextWrapping` now binds to the same helper decision used for fitted font size.
- Raster print text uses the same layout result; no-wrap rotated text is drawn without a separate `MaxTextWidth` wrapping rule.
- Regression probe now asserts 90-degree narrow text uses no-wrap fit and that measured unwrapped width stays inside the content box.
- `dotnet build testbartender.sln --no-restore -v:minimal` passed outside the sandbox with 0 errors.
- `dotnet run --project test_parse.csproj --no-restore` passed outside the sandbox with `Template parity probe passed.`
- Relaunched the rebuilt debug app from `BarTenderClone\bin\Debug\net9.0-windows\BizPro6Barcode.exe` after the final build.
- Existing warnings remain: unreachable NuGet vulnerability metadata source `https://store.iotech.mn/v3/index.json` plus pre-existing nullable warnings.

---

# Rotated text auto-fit inside selection box

## Plan
- [x] Stop auto-growing text local/selection boxes from content measurement.
- [x] Add shared fitted text layout measurement in `DesignerInteractionHelper`.
- [x] Bind designer text rendering to fitted font and explicit box dimensions.
- [x] Use the same fitted layout in raster print rendering.
- [x] Update regression probes for narrow rotated text, designer/print parity, and resize behavior.
- [x] Verify with solution build and `test_parse.csproj`, then record results.

## Notes
- User report: text content still requires dragging the lower middle handle to reveal full content, but expected behavior is content fitting inside the visible blue box.
- Decision: user-controlled box stays fixed; text auto-fits down at render time.

## Review
- Text local/selection size now stays based on model `Width/Height` instead of expanding to measured content width/height.
- Added shared `DesignerInteractionHelper.MeasureTextLayout(...)`, which returns fitted render font size and content bounds for a fixed box.
- Designer text now binds `FontSize` to the fitted layout result; the rotated local grid clips to its explicit box.
- Raster print text rendering now uses the same fitted layout result, preserving designer/print parity for auto-fit text.
- Regression probe now asserts narrow rotated text keeps explicit box size, fits inside that box, keeps designer/print bitmap parity, and preserves existing 8-handle anchor behavior.
- `dotnet build testbartender.sln --no-restore -v:minimal` passed with 0 errors.
- `dotnet run --project test_parse.csproj --no-restore` passed with `Template parity probe passed.`
- Existing warning remains: `NU1900` because `https://store.iotech.mn/v3/index.json` vulnerability metadata is unreachable.

---

# Rotated resize/selection stability fix

## Plan
- [x] Commit measured text local size before drag/resize so model `Width/Height` and designer local box cannot drift apart.
- [x] Rework rotated resize to use local-rect fixed opposite anchors instead of resizing the visual AABB.
- [x] Keep text corner-only font scaling and side-handle box-only behavior.
- [x] Add regression probes for all handle directions at 0/90/180/270, anchor stability, and rotated text pixel bounds.
- [x] Verify with solution build and `test_parse.csproj`, then record results.

## Notes
- User report: rotated selected elements still clip content and move/resize in confusing directions from the 8 blue handles.
- Root cause being addressed: hidden auto-expanded display size diverges from persisted model size, then drag math switches between those coordinate systems.

## Review
- Added `DesignerInteractionHelper.CommitMeasuredLocalSize(...)` and call it before element move/resize starts, so measured text boxes are committed into model `Width/Height` before interaction math begins.
- Replaced resize's visual-AABB mutation path with local fixed-anchor math. Opposite corner/edge remains stable while drag deltas are inverse-rotated into the element's local axes.
- Kept text corner-only `FontSize` scaling; side/top/bottom handles keep font unchanged and resize only the text box.
- Added rotation-aware resize cursors for all handles through `DesignerElementGeometryConverter`.
- Extended `test_parse.csproj` probe to simulate all 8 resize handles at `0/90/180/270`, verify opposite anchor stability, verify model/display size commit, and assert rotated text pixels stay inside the visual box.
- `dotnet build testbartender.sln --no-restore -v:minimal` passed with 0 errors.
- `dotnet run --project test_parse.csproj --no-restore` passed with `Template parity probe passed.`
- Existing warning remains: `NU1900` because `https://store.iotech.mn/v3/index.json` vulnerability metadata is unreachable.

---

# Rotated element geometry and preview parity

## Plan
- [x] Centralize rotated element geometry around unrotated `LabelElement.X/Y/Width/Height`.
- [x] Replace heuristic text display height with measured WPF text bounds so rotated text does not clip.
- [x] Keep designer chrome and resize handles tied to computed rotated screen-space points.
- [x] Align raster print rendering with the same geometry/text measurement rules.
- [x] Extend the template parity probe with rotated geometry and raster nonblank checks.
- [x] Verify with solution build and `test_parse.csproj`.

## Notes
- Rotation remains limited to `0/90/180/270`.
- Default print path remains `WysiwygRaster`; native ZPL remains legacy/fallback.
- Template DTO shape stays unchanged.

## Review
- `DesignerInteractionHelper` now measures wrapped text with WPF `FormattedText`, with the old width/character estimate only as a fallback. This keeps zero-height/auto-height rotated text from collapsing or clipping to a partial glyph run.
- Existing rotated visual-bounds and chrome-point math remains the single designer geometry path; the probe now asserts 90-degree bounds swap, handle placement, and center preservation across rotation changes.
- Raster barcode rendering and the designer barcode converter now generate Code 128 images at the minimum encoder-safe width, retrying wider if BarcodeStandard rejects the requested dimensions. The element clip still controls visible output.
- Added `InternalsVisibleTo("test_parse")` so the parity probe can assert internal geometry/raster invariants without making production helpers public.
- `dotnet build testbartender.sln --no-restore -v:minimal` passed outside the sandbox with 0 errors. Sandbox build failed on WPF `obj` cache permissions; one parallel verification attempt also hit temporary WPF generated-file races, so final verification was run sequentially.
- `dotnet run --project test_parse.csproj --no-restore` passed outside the sandbox with `Template parity probe passed.`
- Existing warnings remain: `NU1900` for unreachable `https://store.iotech.mn/v3/index.json` vulnerability metadata and pre-existing nullable warnings.

---

# Text corner resize font scaling

## Plan
- [x] Add corner-only font scaling for text element resize.
- [x] Keep side/top/bottom text resize as box-only wrap/height adjustment.
- [x] Clamp scaled font sizes to a safe readable range.
- [x] Extend the parity probe for corner-vs-side text resize behavior.
- [x] Verify with solution build and `test_parse.csproj`.

## Notes
- Applies only to `ElementType.Text`.
- Existing rotated resize math remains local-axis and snapshot-based.

## Review
- Text corner resize now scales `FontSize` from the drag-start snapshot using the limiting width/height ratio, clamped to 6-72 px.
- Text side/top/bottom resize leaves `FontSize` unchanged, preserving box-only wrapping and height adjustment.
- The parity probe now asserts corner resize scales font and side resize keeps font stable.
- `dotnet build testbartender.sln --no-restore -v:minimal /p:UseAppHost=false` passed while the app was open; normal `dotnet build testbartender.sln --no-restore -v:minimal` passed after the old app instance closed.
- `dotnet run --project test_parse.csproj --no-restore` passed with `Template parity probe passed.`
- Relaunched the rebuilt app from `BarTenderClone\bin\Debug\net9.0-windows\BizPro6Barcode.exe`; latest process id is `39072`.

---

# Rotated text clipping follow-up

## Plan
- [x] Enforce measured text display bounds even when a text element has an explicit height.
- [x] Add a regression probe for explicit-height rotated text.
- [x] Verify with build and parity probe.
- [x] Relaunch the app with the corrected build.

## Notes
- User screenshot shows rotated price/text content clipped inside the element after rotation.
- Root cause: text with `Height > 0` only used line-height minimum, not full measured wrapped text bounds.

## Review
- `DesignerInteractionHelper.GetLocalSize(...)` now enforces measured wrapped text height for all text elements, including explicit-height text. This prevents rotated text from being clipped inside its own local box.
- Added a parity probe case for a narrow explicit-height `MNT 395,000` text element rotated 90 degrees.
- `dotnet build testbartender.sln --no-restore -v:minimal /p:UseAppHost=false` passed while checking compile, then normal `dotnet build testbartender.sln --no-restore -v:minimal` passed after the app was closed.
- `dotnet run --project test_parse.csproj --no-restore` passed with `Template parity probe passed.`
- Relaunched the rebuilt app from `BarTenderClone\bin\Debug\net9.0-windows\BizPro6Barcode.exe`; latest process id is `57704`.

---

# Rotated text width clipping follow-up

## Plan
- [x] Enforce measured minimum text width for long words and glyph overhang.
- [x] Add padding to designer/raster text drawing so glyphs do not sit on the clip edge.
- [x] Add a regression probe for narrow rotated Cyrillic text.
- [x] Verify with build and parity probe.
- [x] Relaunch the app.

## Notes
- User screenshot shows rotated Cyrillic text still clipped slightly, now on the width/overhang axis rather than only height.

## Review
- `DesignerInteractionHelper.GetLocalSize(...)` now expands text local width to the measured longest unwrapped word plus padding, using bold measurement as the conservative bound.
- Text measured height now uses explicit padding instead of a small multiplier, covering glyph overhang more predictably.
- Added regression checks for narrow rotated `Богино ханцуйтай хар цамц` text expanding both width and height.
- `dotnet build testbartender.sln --no-restore -v:minimal` passed after closing the running app instance that locked `BizPro6Barcode.dll`.
- `dotnet run --project test_parse.csproj --no-restore` passed with `Template parity probe passed.`
- Relaunched the rebuilt app from `BarTenderClone\bin\Debug\net9.0-windows\BizPro6Barcode.exe`; latest process id is `27064`.

---

# Rotated text render inset follow-up

## Plan
- [x] Add a real render inset for designer text so glyphs are not painted on clip edges.
- [x] Apply the same text inset to raster print rendering.
- [x] Make measured text height account for the inset-reduced content width.
- [x] Verify with build and parity probe.
- [x] Relaunch the app.

## Notes
- User screenshot shows remaining clipping at both ends after measured box growth, indicating edge overhang rather than only insufficient bounds.

## Review
- Designer text now renders with a real 4px inset inside its local frame instead of painting directly on the clip edge.
- Raster text rendering uses the same inset for print parity.
- Text height measurement now wraps against inset-reduced content width and adds vertical inset/padding.
- `dotnet build testbartender.sln --no-restore -v:minimal` passed with 0 errors.
- `dotnet run --project test_parse.csproj --no-restore` passed with `Template parity probe passed.`
- Relaunched the rebuilt app from `BarTenderClone\bin\Debug\net9.0-windows\BizPro6Barcode.exe`; latest process id is `47360`.

---

# Push and installer refresh

## Plan
- [x] Rebuild the tracked `3.1.3` installer from the current Release publish output.
- [x] Verify build/probe still pass after packaging.
- [x] Stage only the WYSIWYG/rotation source changes, task notes, and refreshed installer artifact.
- [ ] Commit and push `main` to `origin`.

## Notes
- User request: push the code and update the installer exe.
- Keep OneDrive conflict-copy files (`*-DESKTOP-6LFP9FV*`) out of the commit.
- Current tracked installer target is `dist/installer/BizPro6_Barcode_Setup_3.1.3.exe`.

## Review
- Rebuilt Release self-contained publish and Inno Setup installer via `scripts/build-iexpress-installer.ps1 -Configuration Release -Runtime win-x64 -Version 3.1.3`.
- Updated installer output: `dist/installer/BizPro6_Barcode_Setup_3.1.3.exe`, 48.69 MB.
- `dotnet run --project test_parse.csproj --no-restore` passed with `Template parity probe passed.`
- `dotnet build testbartender.sln --no-restore -v:minimal /p:UseAppHost=false` passed with 0 errors and one expected warning because the running debug app (PID 27476) locks `BizPro6Barcode.exe`.

---

# True WYSIWYG designer-to-print parity

## Plan
- [x] Add a shared label render engine used by raster print and parity probes.
- [x] Apply printer calibration at the final bitmap level: offset, scale, and optional whole-label rotation.
- [x] Keep `WysiwygRaster` as the print-primary path and leave native ZPL visuals as legacy fallback.
- [x] Add per-printer calibration profile storage and UI controls.
- [x] Add a calibration test label and calibrated boundary diagnostics.
- [x] Extend regression probes for render parity and calibration transforms.
- [x] Verify with solution build and `test_parse.csproj`.

## Notes
- Designer canvas remains the expected visual source.
- Element model coordinates are not rewritten by calibration.
- Existing rotated element and text clipping fixes must be preserved.

## Review
- Added `LabelRenderEngine` as the shared WPF drawing path for text, barcode, QR, image, rotation, clipping, and calibration-label rendering.
- `LabelRasterRenderService` now only converts the shared rendered bitmap to `^GFA`; `ZplGeneratorService` uses the rendered bitmap dimensions for `^PW/^LL` and writes an `^FX` diagnostic with template size, DPI, raster size, offset, scale, and whole-label rotation.
- Added per-printer calibration profiles stored under `%LOCALAPPDATA%\BarTenderClone\printer-calibration.json`, with UI controls for X/Y offset, X/Y scale, whole-label rotation, and a `Cal Test` print.
- Added a red dashed calibrated output boundary overlay on the designer canvas. Calibration is applied only to the final print bitmap, never to element coordinates.
- Extended `test_parse.csproj` probe to compare designer-vs-print bitmap pixels with default calibration, verify calibration diagnostics, assert 90-degree whole-label raster dimension swap, and verify calibration-label nonblank output.
- `dotnet build testbartender.sln --no-restore -v:minimal` passed outside the sandbox with 0 errors.
- `dotnet run --project test_parse.csproj --no-restore` passed outside the sandbox with `Template parity probe passed.`
- Relaunched the rebuilt app from `BarTenderClone\bin\Debug\net9.0-windows\BizPro6Barcode.exe`; latest process id is `27476`.

---

# RFID print status sync

## Plan
- [x] Trace the RFID print flow from UI to API and confirm the failing integration point.
- [x] Align `ApiService.UpdatePrintStatusAsync` with the backend `Resource/UpdatePrintStatus` contract.
- [x] Route RFID print success branches through the same print-status update flow.
- [x] Verify with a build and record results.

## Follow-up Plan
- [x] Review `mobicom-barcode/CHANGES_PROMPT.md` against this repo and identify the safe subset to port.
- [x] Harden resource/document parsing so fetch does not silently degrade into null document state.
- [x] Fix fetch-data presentation issues for newly added RFID rows, including the displayed date and branch filter reset/count behavior.
- [x] Extend field binding support across the view model, ZPL generation, and print-time cloning for the new RFID/product fields.
- [x] Improve designer UX by selecting newly added elements and refreshing content when field bindings change.
- [x] Verify with a build and document remaining risks.

## Notes
- Verified backend source in `bizpro6/aspnet-core`: `ResourceAppSevice.UpdatePrintStatus(...)` is the intended endpoint for `isPrint` updates.
- Current client success path uses `AssetSync/PrintAndPushRfid`, which was not found in backend source and does not map to the verified print-status contract.
- Current client `UpdatePrintStatusAsync` posts to `ResourceManager/CreateOrUpdateResource`, which does not match the verified backend endpoint contract.
- `CHANGES_PROMPT.md` is partially applicable. The safe subset is parse/default handling, extra resource fields, field binding refresh, and DataGrid/view-model fixes.
- Self-contained single-file publish settings were intentionally excluded from this pass because the current installer pipeline is script-driven and should not be destabilized without a dedicated packaging pass.

## Review
- Verified backend contract from local source: `Bpm.Application/Resource/ResourceAppSevice.cs` exposes `POST /api/services/app/Resource/UpdatePrintStatus` and updates `isPrint` by RFID.
- `dotnet build testbartender.sln` passed after rerunning with elevated permissions due sandbox temp-file access issues.
- Build produced pre-existing nullable warnings, but no new errors from this fix.
- The safe subset from `CHANGES_PROMPT.md` is now implemented in the active repo: robust JSON hydration, extra resource fields, filter/date fixes, field-binding refresh, and DataGrid usability improvements.
- Self-contained single-file publish settings remain intentionally excluded from this task; packaging should be handled as a separate installer-focused change.

---

# Repo readiness

## Plan
- [x] Review local workflow notes in `AGENTS.md`, `tasks/lessons.md`, and the existing `tasks/todo.md`.
- [x] Inspect the solution entry points, service registration, and primary MVVM navigation flow.
- [x] Read the core printing, ZPL generation, API, and resource-model code paths to build a working mental model.
- [x] Record the project map and readiness notes for the next task.

## Notes
- The repo is a single-project WPF app targeting `.NET 9` in `BarTenderClone/BarTenderClone.csproj`; there is one solution project in `testbartender.sln`.
- App startup is host-based in `BarTenderClone/App.xaml.cs`, with DI registrations for auth, API, ZPL, printing, template persistence, metadata services, and the main window/view models.
- `MainViewModel` is just a shell: it starts on `LoginViewModel`, then swaps to `LabelPreviewViewModel` after login, and routes session expiry back to login.
- `LabelPreviewViewModel` is the main orchestration point for the product grid, filtering/pagination, template editing, printer selection, single-item printing, batch RFID printing, and backend status sync.
- `ApiService` handles resource discovery/fallback across candidate API hosts, hydrates `ResourceItem` from dynamic JSON, and posts print status updates to `Resource/UpdatePrintStatus`.
- `PrintService` owns printer enumeration, single-label printing, RFID sequential encoding, per-label tracking, and batch processing through `RawPrinterHelper`.
- `ZplGeneratorService` converts canvas elements into ZPL, including DPI-aware coordinate conversion, barcode/QR generation, UTF-8 mode, media settings, and optional RFID write commands.
- `ResourceItem`/`ResourceDocument` in `BarTenderClone/Models/ResourceModels.cs` are central to data binding; they flatten raw JSON into stable field accessors used by both the grid and label field binding.
- `README.md` is still the default GitLab template, so the actionable project documentation is effectively `AGENTS.md` plus the code.

## Review
- I now have enough repo context to work directly on issues in the UI flow, printing path, API integration, template/designer behavior, and resource parsing without re-discovering the basics.
- Highest-leverage files for future changes are `BarTenderClone/ViewModels/LabelPreviewViewModel.cs`, `BarTenderClone/Services/PrintService.cs`, `BarTenderClone/Services/ZplGeneratorService.cs`, `BarTenderClone/Services/ApiService.cs`, and `BarTenderClone/Models/ResourceModels.cs`.
- No code behavior was changed in this pass; this was a repository familiarization step only.

---

# Preview vs print mismatch

## Plan
- [x] Trace the preview rendering path for text and barcode elements in `LabelPreviewView.xaml` and the preview converters.
- [x] Trace the print rendering path in `ZplGeneratorService` and compare layout math against the preview path.
- [x] Identify the concrete root causes of the preview/print drift and implement the lowest-risk fixes that bring both paths closer together.
- [x] Verify with a build and capture residual risks.

## Notes
- The barcode preview was not using the active printer DPI. `BarcodeToImageConverter` used a hardcoded `300` DPI, while the print path uses the selected DPI (`203/300/600`). This makes preview barcode width and centering drift from the real printed barcode.
- The barcode preview also drew a synthetic image with extra top/bottom padding, while the ZPL path prints the barcode at the full requested height. That makes elements placed under the barcode look farther away in the designer than on paper.
- Preview and print were calculating barcode occupancy in two separate places. That duplication made it easy for the preview to fall out of sync with the ZPL path.
- Text elements with an explicit `Height` were respected by the print path but ignored by the preview `TextBlock`, so resized text boxes could wrap/clip differently between designer and printer.
- The ZPL generator was also applying a much stricter line cap than the preview showed. That makes wrapped text behave differently even when the user sees enough room in the designer.
- A residual limitation still exists: the preview uses Windows text rendering while print uses Zebra-resident ZPL fonts, so exact glyph shape and bold weight still cannot be perfectly WYSIWYG without changing the printing strategy.

## Review
- Added a shared Code 128 layout helper in `LabelSizeHelper` so the preview and ZPL generator now use the same barcode width/module calculation.
- Updated `BarcodeToImageConverter` to use the active printer DPI, the element centering flag, and full-height rendering without the extra visual padding that had been misleading the designer.
- Updated the preview text element in `LabelPreviewView.xaml` to honor explicit text height via a `ZeroToAutoLengthConverter`, so resized text boxes behave closer to print-time layout.
- Relaxed the print-time text line cap in `ZplGeneratorService` so the printer no longer imposes a surprise 1-line/3-line limitation that the preview never showed.
- `dotnet build testbartender.sln` passed after rerunning outside the sandbox because WPF markup compilation could not write to `obj` inside the sandbox.
- Residual risk: text font appearance can still differ slightly because preview uses WPF/system fonts and printer output uses Zebra font `0`. If exact text shape parity is required, the next step is to standardize on a downloadable font or print text as graphics.

---

# Session warmup 2026-04-14

## Plan
- [x] Re-read workflow notes and existing repo readiness context before starting.
- [x] Re-validate the main startup, navigation, data, and print flows against the current code.
- [x] Verify the current solution baseline with a fresh build.

## Notes
- Current dirty worktree includes pre-existing edits in `BarTenderClone/App.xaml`, `BarTenderClone/Services/ZplGeneratorService.cs`, `scripts/build-iexpress-installer.ps1`, `setup/installer.iss`, and `.claude/settings.local.json`; those should be treated as user-owned unless a later task says otherwise.
- The main operational hub remains `BarTenderClone/ViewModels/LabelPreviewViewModel.cs`, with API/resource hydration in `BarTenderClone/Services/ApiService.cs`, print orchestration in `BarTenderClone/Services/PrintService.cs`, and label rendering in `BarTenderClone/Services/ZplGeneratorService.cs`.
- `dotnet build testbartender.sln` failed inside the sandbox because MSBuild could not create a temp file under `BarTenderClone/obj`, but succeeded when rerun outside the sandbox.

## Review
- Build baseline is currently green: `dotnet build testbartender.sln` succeeded with warnings only.
- The most visible existing cleanup candidates are nullable warnings in view models/models/converters and one unreachable-code warning in `BarTenderClone/Services/ZplGeneratorService.cs`.
- Repo context is fresh enough to move directly into bug fixing or feature work without another discovery pass.

---

# Row numbering scroll investigation

## Plan
- [x] Locate the UI component that renders the data list and identify how row numbering is computed.
- [x] Check whether virtualization, pagination, sorting, or collection replacement can cause the displayed number to drift during scroll.
- [x] Decide whether the reported issue is a real defect, document the root cause, and outline the cleanest fix.

## Notes
- The row-number column in `BarTenderClone/Views/LabelPreviewView.xaml` is not bound to item data; it is bound to `DataGridRow`'s `ItemsControl.AlternationIndex` and then incremented by `IncrementConverter`.
- The grid is paged through `Products`, which is rebuilt from `FilteredProducts.Skip(Pagination.StartIndex).Take(Pagination.PageSize)` in `LabelPreviewViewModel.UpdatePagedView()`.
- Pagination already knows the absolute offset through `Pagination.StartIndex`, but the current row-number rendering ignores that offset completely.
- Microsoft documents that WPF `DataGrid.EnableRowVirtualization` defaults to `true`, which means `DataGridRow` containers are created only for visible items and are recycled as the user scrolls. Using a recycled row container's alternation metadata for a business-visible row number is brittle even when it appears to work.

## Review
- The reported issue is credible and the current implementation is objectively the wrong primitive for stable numbering.
- Even if the visible numbers sometimes look correct, the code currently produces page-local numbering at best and container-driven numbering at worst; it is not tied to the actual item identity or absolute display position.
- The cleanest fix is to compute row numbers from the real display index plus `Pagination.StartIndex`, instead of using `AlternationIndex` for the `#` column.

---

# Row numbering fix

## Plan
- [x] Confirm whether sorting needs to remain supported by the row-number solution.
- [x] Replace the row-number binding so it uses the actual displayed row index plus page offset.
- [x] Verify the change with a solution build and record the result.

## Notes
- The `#` column in `BarTenderClone/Views/LabelPreviewView.xaml` now reads from the hosting `DataGridRow.Tag` instead of `ItemsControl.AlternationIndex`.
- `BarTenderClone/Views/LabelPreviewView.xaml.cs` now updates that tag from `Pagination.StartIndex + row.GetIndex() + 1` during `LoadingRow`, so numbering is tied to the row's actual displayed position on the current page.
- A `Sorting` handler refreshes visible row tags after DataGrid sorting completes, so the numbers stay aligned even when the current page is re-ordered by a sortable column.
- The old `IncrementConverter` resource was removed from `App.xaml`; the unused converter source file remains harmless but is no longer part of the active row-numbering path.

## Review
- `dotnet build testbartender.sln` succeeded after the fix with warnings only and no new build errors.
- The new implementation is stable under pagination because it explicitly includes `Pagination.StartIndex`.
- The new implementation is stable under virtualization because row numbers are assigned when WPF prepares each visible `DataGridRow`, rather than inferred from recycled alternation metadata.

---

# Preview vs print production hardening

## Plan
- [x] Re-check the first pass and remove any remaining synthetic or heuristic rendering that could still drift from print output.
- [x] Use the actual barcode library in the preview path instead of a fake placeholder renderer.
- [x] Rebuild after the hardening pass and capture the remaining residual risk honestly.

## Notes
- The first pass aligned barcode sizing math and text wrapping behavior, but the preview barcode image was still a synthetic placeholder. That is not good enough for a production-critical WYSIWYG issue.
- The repo already ships `BarcodeLib` (`BarcodeStandard`) and its SkiaSharp dependency, so the correct hardening move was to use the real Code 128 renderer in the preview path rather than a pseudo-random bar pattern.
- `BarcodeToImageConverter` now composes a real `BarcodeStandard.Barcode` Code128 image into the element box, while still honoring the shared width math, selected printer DPI, and `IsCentered`.
- The shared layout math in `LabelSizeHelper.CalculateCode128Layout(...)` remains the single source of truth for preview/print barcode occupancy.

## Review
- `dotnet build testbartender.sln` passed after the production-hardening pass with warnings only.
- The preview barcode is now materially closer to the physical output because it uses a real barcode renderer instead of a dummy visual approximation.
- The remaining known gap is text glyph parity: WPF preview text still uses Windows font rendering while print uses Zebra font `0`. Positioning and wrapping are much closer now, but perfect shape/weight parity would require printer-font standardization or printing text as graphics.

---

# Production packaging push

## Plan
- [x] Review the dirty worktree and separate production-bound source changes from local-only Codex files.
- [x] Rebuild the application/installer on the combined worktree to make sure both sessions' changes still compile together.
- [x] Stage only the intended source and packaging files, then create a release-ready commit.
- [x] Push the refreshed state to GitHub.

## Notes
- Local-only files currently present in the worktree are `.claude/settings.local.json`, `.claude/worktrees/`, and the `tasks/` directory itself; those should stay out of the Git commit.
- Production-bound source changes for this push are the preview/print parity fixes, the row-numbering fix from the other Codex session, and the installer pipeline updates in `scripts/build-iexpress-installer.ps1` plus `setup/installer.iss`.
- The installer was rebuilt successfully on the combined worktree with `scripts/build-iexpress-installer.ps1`, producing `dist/installer/BizPro6_Barcode_Setup_3.0.0.exe`.

## Review
- `dotnet build testbartender.sln` succeeded on the combined worktree with `0` warnings and `0` errors when rerun outside the sandbox.
- Release commit created on `main`: `ee8ebd5 Fix label preview parity and refresh installer packaging`.
- Pushed successfully to `origin/main`.
- Local-only artifacts still intentionally remain uncommitted: `.claude/settings.local.json`, `.claude/worktrees/`, and `tasks/`.

---

# Session warmup 2026-04-28

## Plan
- [x] Review local workflow notes and existing readiness context before making changes.
- [x] Inspect current git state and project structure without relying on `rg`, which is blocked in this environment.
- [x] Re-read the main startup, navigation, resource/API, ZPL generation, and print-service paths.
- [x] Verify the current solution baseline with a build if the local environment allows it.
- [x] Record readiness notes and any current risks for the next task.

## Notes
- `rg --files` is currently blocked with `Access is denied`; use PowerShell `Get-ChildItem` and `Select-String` until that is resolved.
- The active worktree is dirty and contains many OneDrive conflict-copy files named `*-DESKTOP-6LFP9FV.*`. Because SDK-style C# projects include `*.cs` by default, those conflict copies are compiled and create duplicate type errors.
- `dotnet build testbartender.sln` failed inside the sandbox due denied temp-file access under `BarTenderClone/obj`; rerunning outside the sandbox restored packages but failed with 389 compile errors caused primarily by duplicate source files.
- The active code has partially applied metadata/session changes: `LabelPreviewViewModel` now requires `IResourceMetadataService` and `ILoggingService`, while `App.xaml.cs` still constructs it with only the older dependencies.
- `LabelPreviewViewModel` references `_sessionService.IsTokenExpired`, but the active `ISessionService` only exposes `AccessToken`, `TenantId`, `ApiBaseUrl`, and `IsAuthenticated`. A conflict-copy version includes `TokenExpiresAt`/`IsTokenExpired`.
- `ResourceMetadataService` is the metadata service currently referenced by `LabelPreviewViewModel`; `FieldMetadataService` and `TenantMetadataService` also exist, but the active app startup does not register any metadata service.
- `BarcodeToImageConverter` in the active source is still a synthetic/random barcode preview renderer, while `ZplGeneratorService` calculates actual ZPL barcode output separately. Treat preview/print parity as suspect until conflict copies and source selection are reconciled.

## Review
- I am ready to work on the app's login/API/resource loading, field metadata, designer preview, ZPL generation, print service, and RFID print-status sync paths.
- Before feature or bug work, the highest-leverage cleanup is to resolve or exclude the `*-DESKTOP-6LFP9FV.*` conflict files, then align DI/session interfaces so the active source can compile.
- I did not change application behavior in this warmup; only `tasks/todo.md` was updated with the plan and readiness findings.

---

# Price zero investigation for RFID DC7C36B7

## Plan
- [x] Find RFID `DC7C36B7` in local API traces/sample payloads and identify the exact JSON shape Bizpro returns.
- [x] Trace how price is parsed from API JSON into `ResourceItem.Price`, including top-level joins and nested `document`.
- [x] Compare parser expectations against the actual field names/types used by the affected item.
- [x] Identify the root cause and write a concrete fix plan with verification steps before implementation.

## Notes
- `DC7C36B7` was not present in `api_latest_response.txt` or `C:\Temp\api_response.txt`; the cached login token in `C:\Temp\login_response.txt` expired on `2026-04-18T01:32:02Z`, so the exact live row cannot be fetched from the local traces without logging in again.
- `C:\Temp\api_response.txt` is a live Bizpro trace against `https://bizpro.mn` for `key=tms_product_rfid`, `skip=21000`, `take=1000`.
- That trace has 661 parsed rows; 216 rows have `cost` missing/zero while at least one display-price field (`price`, `discountPrice`, or `currency`) is non-zero.
- Example shape from the trace: `tms_product.cost = "0"`, `tms_product.price = 330000`, `tms_product.discountPrice = 330000`, `tms_product.currency = "300000"`. The current app would display `0` because it chooses `cost` first.
- Current active code maps `ResourceItem.Price` to `ParsedDocument.Product.Cost`.
- Current active `ProductDto.Cost` uses `CostRaw ?? PriceRaw ?? CurrencyRaw`, so it only falls back when `cost` is null. It does not fall back when `cost` is present but semantically empty/zero.
- The UI and ZPL paths both use `product.Price`/`dataSource.Price`, so the parsing bug affects the grid, designer field binding, generated labels, and printed ZPL consistently.

## Root Cause
- The app is treating Bizpro `cost` as the label/display price and prioritizing it over `price`/`discountPrice`/`currency`.
- Bizpro payloads can contain `cost: 0` while the actual Bizpro display price is in `price` or `discountPrice` (and sometimes sale/base price in `currency`).
- Because fallback is null-only instead of value-aware, a present numeric `0` masks the valid non-zero Bizpro price.

## Fix Plan
- First clean the build-blocking OneDrive conflict files from the active compile set, otherwise verification cannot be trusted.
- Add `DiscountPriceRaw` to `ProductDto` with `[JsonProperty("discountPrice")]`.
- Separate purchase cost from display price in the model instead of overloading `Cost`:
  - Keep `Cost` as parsed `cost`.
  - Add `DisplayPrice` or `SalePrice` that resolves from `discountPrice`, then `price`, then `currency`, then only falls back to non-zero `cost`.
  - Parse numbers using invariant/culture-tolerant logic that handles numeric JSON values, numeric strings, commas, and currency symbols.
- Change `ResourceItem.Price` to return the new display-price property.
- Update dynamic field resolution so `PRICE` maps to display price, while optional `Cost`/`Currency` fields remain available separately if needed.
- Verify with a focused parser test or small harness using:
  - `cost: "0", price: 121000, discountPrice: 121000, currency: "110000"` -> display price `121000`.
  - `cost: "0", price: 330000, discountPrice: 330000, currency: "300000"` -> display price `330000`.
  - legacy rows with only `cost` non-zero -> still display that non-zero value.
  - malformed/empty price fields -> display `0` without throwing.
- Then run `dotnet build testbartender.sln` after the compile set is cleaned.

---

# Price display fix implementation

## Plan
- [x] Exclude OneDrive conflict-copy files such as `*-DESKTOP-6LFP9FV.*` from the WPF project compile/page/content set without deleting user files.
- [x] Implement a Bizpro display-price resolver that treats zero `cost` as empty when `discountPrice`, `price`, or `currency` has a valid non-zero value.
- [x] Keep purchase `cost` available separately from label/display `Price` so future bindings do not conflate the two.
- [x] Align current DI/session code so the app builds with the active `LabelPreviewViewModel` constructor and session expiry checks.
- [x] Build the solution and audit the touched paths for regressions.
- [x] Record verification results and residual risk.

## Notes
- Root cause was confirmed in `C:\Temp\api_response.txt`: Bizpro rows can return `tms_product.cost = "0"` while real display-price fields such as `price`, `discountPrice`, or `currency` are non-zero.
- `ResourceItem.Price` now uses `ProductDto.DisplayPrice`, resolved as first positive value from `discountPrice`, `price`, `currency`, then non-zero `cost`.
- `ResourceItem.Cost` remains separate and maps only to Bizpro `cost`, so display price and purchase cost are no longer conflated.
- The `PRICE` binding still formats from `ResourceItem.Price`, so the grid, designer field binding, ZPL generation, and printed label all follow the new display-price resolver.
- OneDrive conflict-copy files remain on disk but are excluded from `Compile`, `Page`, `None`, and `Content` items to prevent duplicate type and XAML build failures.
- `AssemblyName` and `Product` project metadata were preserved as `BizPro6Barcode` / `BizPro6 Barcode` so packaging output name is not changed by this fix.

## Review
- Sample audit against `C:\Temp\api_response.txt`: 661 rows parsed; 216 rows matched the old bug pattern (`cost` zero/missing but Bizpro price field non-zero); 0 of those remain zero with the new resolver.
- The exact RFID `DC7C36B7` was not present in the available cached traces, and the cached login token expired on `2026-04-18T01:32:02Z`, so live row verification needs a fresh login session.
- `dotnet build testbartender.sln` succeeds and outputs `BarTenderClone\bin\Debug\net9.0-windows\BizPro6Barcode.dll` with 0 errors.
- `dotnet build testbartender.sln -c Release` succeeds and outputs `BarTenderClone\bin\Release\net9.0-windows\BizPro6Barcode.dll` with 0 errors.
- Remaining build warnings are pre-existing nullable/converter/adorner warnings and one existing unreachable-code warning in `ZplGeneratorService`; no price-resolution, print-guard, or DI/session compile blockers remain.

---

# API host smoke-test failure

## Plan
- [x] Compare the runtime error host list against the checked-in API configuration.
- [x] Restore the intended primary/fallback API hosts without changing price parsing.
- [x] Rebuild and restart the app so the user can retest live data.
- [x] Record the result and whether the failure was caused by the price fix.

## Notes
- The screenshot error only tried `https://api.bizpro.mn`, where `tms_product_rfid` and `product_rfid` were reported as missing resources.
- The successful cached Bizpro trace used `https://bizpro.mn`, and HEAD config also uses `https://bizpro.mn` as primary with `https://api.bizpro.mn` as fallback.
- Local `BarTenderClone/appsettings.json` had drifted to only `https://api.bizpro.mn`; this explains the load failure independently of the price resolver.

## Review
- Restored `BarTenderClone/appsettings.json` to primary `https://bizpro.mn`, fallback `https://api.bizpro.mn`, and OIDC preferred API `https://bizpro.mn`.
- Verified `BarTenderClone\bin\Debug\net9.0-windows\appsettings.json` contains the restored config after build.
- `dotnet build testbartender.sln` passed with 0 warnings and 0 errors.
- Restarted the app; new process is running with corrected config.

---

# Session readiness 2026-05-04

## Plan
- [x] Review local workflow notes, `tasks/lessons.md`, and prior readiness context.
- [x] Inspect current git/worktree state and confirm how OneDrive conflict-copy files affect the compile set.
- [x] Re-read startup/DI, session, API, resource parsing, preview, ZPL, and print paths enough to support follow-up bug fixes.
- [x] Verify the current solution baseline with `dotnet build testbartender.sln`.
- [x] Record active risks and readiness notes.

## Notes
- `rg --files` is still blocked in this environment with `Access is denied`; use PowerShell `Get-ChildItem` and `Select-String` for repo search until that changes.
- The worktree is dirty. There are tracked changes plus many untracked OneDrive conflict-copy files named `*-DESKTOP-6LFP9FV*`.
- `BarTenderClone/BarTenderClone.csproj` currently excludes `*-DESKTOP-6LFP9FV*` from `Compile`, `Page`, `None`, and `Content`, so the conflict copies stay on disk without breaking the WPF build.
- The active app is a single WPF `.NET 9` project. Startup is host/DI based in `BarTenderClone/App.xaml.cs`, and navigation runs from `LoginViewModel` to `LabelPreviewViewModel` through `MainViewModel`.
- `ISessionService` and `SessionService` now include `TokenExpiresAt` and `IsTokenExpired`, matching the active `LabelPreviewViewModel` session-expiry checks.
- `ResourceItem.Price` now maps to `ProductDto.DisplayPrice`, which resolves the first positive value from `discountPrice`, `price`, `currency`, then non-zero `cost`; `ResourceItem.Cost` remains separate.
- `ApiService` tries configured/candidate API hosts, fetches RFID resources by resource key, hydrates dynamic JSON into `ResourceItem`, and posts print status to `Resource/UpdatePrintStatus`.
- `PrintService` handles printer discovery, ZPL send, spooler tracking, RFID sequential data, and batch/single print orchestration.
- `ZplGeneratorService` owns screen-pixel to printer-dot conversion, ZPL text/barcode/QR output, and optional RFID write commands.
- Active `BarcodeToImageConverter` still draws a synthetic barcode preview with random bars seeded by content. That does not match the earlier production-hardening note that preview should use the real barcode renderer.
- `LabelSizeHelper.FONT_SCALING_FACTOR` is currently `1.5`, while the project guidance text says it is `1.0`; treat preview/print text sizing as an area to verify before changing label layout.
- There is an unfinished `API host smoke-test failure` plan above this section. I did not change or resolve it during this readiness pass.

## Review
- `dotnet build testbartender.sln` succeeds with 0 warnings and 0 errors, outputting `BarTenderClone\bin\Debug\net9.0-windows\BizPro6Barcode.dll`.
- I am ready to work on login/API host issues, resource parsing, price/status fields, designer preview, ZPL generation, print/RFID behavior, and packaging from this baseline.
- The highest-risk current areas are live API host selection and barcode/text preview-vs-print parity, not basic compilation.

---

# Template print mismatch investigation

## Plan
- [x] Trace template save/load to confirm which `LabelElement` fields are persisted and restored.
- [x] Compare preview rendering in `LabelPreviewView.xaml`/converters with print rendering in `ZplGeneratorService`.
- [x] Check whether selecting products or auto-populate logic mutates template element positions, sizes, field bindings, or contents before printing.
- [x] Verify the current baseline with a build after read-only inspection.
- [x] Present root cause candidates and a fix plan before making any code changes.

## Notes
- User report: RFID print output looks different from the configured template.
- User explicitly asked to diagnose and present the plan first, not to fix immediately.
- This is a real issue. The strongest root cause is that preview/data refresh displays RFID with leading zeros stripped, while ZPL print resolves the `RFID` field from raw `dataSource.Rfid`. Barcode width is data-length sensitive, so the printed barcode can be much wider or centered differently than the preview.
- `BarcodeToImageConverter` still renders synthetic/random bars and uses hardcoded `DEFAULT_DPI = 300`; print uses selected printer DPI and real ZPL `^BC`. This makes barcode preview unreliable, especially on 203 DPI printers.
- Text preview and text print use different capabilities. Preview honors `IsBold`, `Width`, and centered `TextAlignment`; ZPL text generation ignores `IsBold`, ignores `Height`, and only uses `Width` when `IsCentered` is true.
- Template persistence drops `IsCentered`: `LabelElementDto` lacks the property, and `TemplateService` does not save/load it. Centering can be lost after saving/loading a template.
- Local sample template `New Template.btl` contains negative X coordinates. Dragging clamps negative positions, but load/property input/validation do not; ZPL can receive negative `^FO` positions and print/crop differently than the preview.
- Field binding ComboBox binds object items (`ResourceFieldOption`) to a string `FieldName` through `SelectedItem` instead of `SelectedValuePath=Key`; this can make template field selection state fragile.
- Product selection with an existing template does not appear to mutate layout; it refreshes only bound element content. Auto-populate clears/rebuilds layout only when the canvas is empty.

## Proposed Fix Plan
- Add a single shared display resolver for label visual fields and make preview refresh plus ZPL visual generation use it. For RFID visual barcode/text, use the same stripped display value; keep raw RFID only for `^RFW` RFID encoding.
- Replace synthetic `BarcodeToImageConverter` with real Code128 preview rendering or a shared layout helper that takes `PrinterDpi`, `IsCentered`, `Width`, and `Height`; bind the converter to `PrinterDpi` and `IsCentered`.
- Persist all layout-affecting properties in templates, at least `IsCentered` now and any future print-affecting style fields.
- Make ZPL text generation honor the same template semantics the preview exposes: bold mapping, field block width for wrapping/clipping, alignment, and a line count derived from element height.
- Validate/clamp left/top negative positions on load and before print, and warn for content outside any edge, not only right/bottom.
- Fix field-binding ComboBox to use `DisplayMemberPath="DisplayName"` and `SelectedValuePath="Key"` with `SelectedValue` bound to `FieldName`.
- Add a small regression harness/test that loads a representative template and compares preview-resolved field values/layout inputs against the generated ZPL coordinates/data for 203 and 300 DPI.

## Review
- `dotnet build testbartender.sln` passed with 0 warnings and 0 errors after read-only inspection.
- No application code was changed during this investigation; only this task note was updated.

---

# Template print mismatch production fix

## Plan
- [x] Create a shared field/value resolver so designer preview content and ZPL visual output use the same display values, while RFID encoding still uses raw RFID.
- [x] Replace synthetic barcode preview behavior with deterministic Code128 layout/rendering that uses selected printer DPI and the same sizing inputs as ZPL.
- [x] Persist all print-affecting template properties currently exposed in the designer, starting with `IsCentered`.
- [x] Harden template/print validation for negative and out-of-bounds coordinates.
- [x] Fix field binding selection so the UI writes stable field keys to `LabelElement.FieldName`.
- [x] Add focused regression coverage for RFID display-vs-encode behavior and template persistence.
- [x] Verify with build and document residual risks.

## Notes
- User requested the best safe production-ready fix, not just a quick patch.
- Keep changes scoped to rendering parity and template persistence; do not alter API/price/auth behavior unless compile requires it.
- Added `LabelFieldValueResolver` so preview refresh and ZPL visual rendering use the same display values. RFID visual output now strips leading zeros consistently, while `^RFW` encoding still uses the raw RFID value.
- Added shared Code128 layout metrics in `LabelSizeHelper` and updated barcode preview to render real Code128 via `BarcodeStandard`/SkiaSharp using selected printer DPI and element centering.
- Updated text preview and ZPL text output to agree more closely on width, height-derived line count, centering, and bold approximation.
- Added template persistence for `IsCentered`, plus Center/Bold controls in the element properties panel.
- Template load now clamps negative X/Y/Width/Height to non-negative values, and print validation warns about negative/out-of-bounds elements.
- Fixed field binding ComboBox to display field names and write stable field keys through `SelectedValuePath="Key"`.
- Reworked `test_parse.csproj`/`Program.cs` into a focused template parity probe.
- Final audit also hardened the RFID display helper so missing/blank RFID values are not coerced into a real-looking `"0"` barcode value. All-zero RFID still displays as `"0"`.

## Review
- `dotnet run --project test_parse.csproj` passed: visual RFID uses stripped display value, RFID encoder uses raw RFID, negative ZPL coordinates are not emitted, and `IsCentered` survives template DTO roundtrip.
- `dotnet build testbartender.sln` passed with 0 warnings and 0 errors after stopping the running `BizPro6Barcode` process that was locking the debug exe.
- `dotnet build testbartender.sln -c Release` passed with 0 errors. It reports existing nullable warnings in `LoginViewModel`, `LoginModels`, `ResizeAdorner`, and `QrCodeToImageConverter`; no new warnings came from the template parity changes.
- Final audit rerun passed after the missing-RFID guard: `dotnet run --project test_parse.csproj`, `dotnet build testbartender.sln` (0 warnings/0 errors), and `dotnet build testbartender.sln -c Release` (0 errors, same existing nullable/converter warnings).
- Residual limitation: WPF preview text still cannot perfectly match Zebra resident font glyph shapes. The fix makes data, positioning, field block width, line count, alignment, and barcode sizing consistent; exact glyph shape would require downloadable printer fonts or rendering text as graphics.

---

# Installer release and push 2026-05-04

## Plan
- [x] Normalize the git index so stale staged deletions do not get committed accidentally.
- [x] Stage only production-relevant source, template parity regression harness, installer configuration/script, and the regenerated installer artifact.
- [x] Rebuild Debug/Release and regenerate the installer executable, overriding the previous local installer output.
- [x] Commit the selected changes on `main` and push to `origin/main`.
- [x] Record verification results and any files intentionally left uncommitted.

## Notes
- The worktree contains many OneDrive conflict-copy files named `*-DESKTOP-6LFP9FV*`; these are local artifacts and should not be pushed.
- Local `.claude/` files and `tasks/` notes are operational context, not production application files.
- User corrected the packaging direction to Inno Setup. `scripts/build-iexpress-installer.ps1` now uses `ISCC.exe`; the filename is legacy but the toolchain is Inno Setup.
- Inno Setup compiler used: `C:\Users\mooji\AppData\Local\Programs\Inno Setup 6\ISCC.exe`.
- Regenerated installer: `dist\installer\BizPro6_Barcode_Setup_3.1.0.exe`, size 48.65 MB, SHA256 `2E0279168AE33E24603E45B08186F515CF3D7481AB996C0871033E930BC226C0`.

## Review
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-iexpress-installer.ps1 -Version 3.1.0` succeeded using Inno Setup and regenerated `BizPro6_Barcode_Setup_3.1.0.exe`.
- `dotnet run --project test_parse.csproj` passed after packaging.
- `dotnet build testbartender.sln` passed with 0 errors; existing nullable/converter warnings remain.
- Commit pushed to `origin/main`: `758d65b Fix RFID template parity and refresh Inno installer`.
- Left uncommitted intentionally: OneDrive conflict-copy files (`*-DESKTOP-6LFP9FV*`), `.claude/`, and local `tasks/` notes.

---

# Logo/image insert and rotation feature

## Plan
- [x] Add template/model fields for embedded image data and safe rotation values.
- [x] Add Insert Image command that embeds a normalized PNG directly in the template.
- [x] Render embedded images in designer preview and expose image elements in the element type list.
- [x] Add 0/90/180/270 rotation support for text, barcode, QR, and image elements.
- [x] Generate inline ZPL `^GFA` graphics for image elements and keep images aspect-preserving.
- [x] Extend regression probe, build, rebuild Inno installer, and smoke-launch the app.

## Notes
- Image data is embedded in `.btl`; no backend/database upload is required.
- Large source images are normalized to PNG with a maximum 800px dimension before template embedding.
- Rotation intentionally supports only 0/90/180/270 degrees for ZPL-native text/barcode/QR output and safer barcode scan behavior.

## Review
- `dotnet run --project test_parse.csproj` passed with assertions for embedded image roundtrip, `^GFA` output, and native ZPL rotation commands.
- `dotnet build testbartender.sln` passed with 0 warnings and 0 errors.
- Inno installer rebuild passed and generated `dist\installer\BizPro6_Barcode_Setup_3.1.0.exe`.
- Smoke launch passed: rebuilt `dist\publish\BizPro6Barcode.exe` started and was responding.

---

# Label design to ZPL command audit 2026-05-05

## Plan
- [x] Audit label size, element coordinate conversion, and ZPL command generation.
- [x] Check rotation parity for text, barcode, QR, and image elements.
- [x] Add regression probe for any mismatch found during audit.
- [x] Rebuild solution and installer after the parity fix.
- [x] Smoke-launch the rebuilt published app.

## Notes
- Existing label size output uses `^PW` and `^LL` from template screen pixels converted through `LabelSizeHelper.ScreenPixelsToDots`.
- Element locations use `^FO` with the same screen-to-dot conversion path and clamp negative coordinates to zero.
- Text, barcode, and QR rotation use ZPL native orientation values `N/R/I/B`.
- Audit found one real issue: rotated image graphics were scaled to the element box before rotation, so 90/270 degree rectangular images could emit swapped `^GFA` dimensions compared with the designer element box.
- Fixed image conversion to rotate the source first, then fit it into the requested element width/height before producing `^GFA`.

## Review
- `dotnet run --project test_parse.csproj` passed, including the new rectangular rotated image `^GFA,16,16,2,` dimension assertion.
- `dotnet build testbartender.sln` passed with 0 warnings and 0 errors.
- Inno installer rebuild passed and generated `dist\installer\BizPro6_Barcode_Setup_3.1.0.exe`, SHA256 `03D855DCBC56A12C203E5F9DE03E52ABF133068D67D6238C1D80795C5D8D8D84`.
- Smoke launch passed: rebuilt `dist\publish\BizPro6Barcode.exe` started and was responding.

---

# Session readiness 2026-05-07

## Plan
- [x] Review local workflow notes, `tasks/lessons.md`, and prior readiness context.
- [x] Inspect current git/worktree state and project structure.
- [x] Re-read startup/DI, session, API/resource parsing, designer/template, ZPL generation, and print/RFID paths.
- [x] Verify the current baseline with the template parity probe and solution build.
- [x] Record readiness notes and active risks.

## Notes
- `rg --files` is still blocked in this environment with `Access is denied`; use PowerShell `Get-ChildItem` and `Select-String` for repo search.
- The active app is a single WPF `.NET 9` project in `BarTenderClone/BarTenderClone.csproj`, outputting `BizPro6Barcode`.
- `BarTenderClone/BarTenderClone.csproj` excludes OneDrive conflict-copy files named `*-DESKTOP-6LFP9FV*` from compile/page/content items, so those local files remain on disk without breaking builds.
- The current worktree is dirty: tracked deletion of `dist/installer/BizPro6_Barcode_Setup_3.1.0.exe`, untracked `dist/installer/BizPro6_Barcode_Setup_3.1.2.exe`, untracked OneDrive conflict-copy files, `.claude/`, and `tasks/`.
- App startup is host/DI based in `BarTenderClone/App.xaml.cs`; navigation runs from `LoginViewModel` to `LabelPreviewViewModel` through `MainViewModel`.
- API config currently uses `https://bizpro.mn` as primary and `https://api.bizpro.mn` as fallback.
- `ApiService` tries configured/candidate API hosts, fetches RFID resources by known or discovered resource keys, hydrates `ResourceItem`, validates tenant consistency, and updates print status through `Resource/UpdatePrintStatus`.
- `ResourceItem.Price` maps to `ProductDto.DisplayPrice`, which prefers first positive `discountPrice`, `price`, `currency`, then non-zero `cost`; `Cost` remains separate.
- `LabelPreviewViewModel` owns data loading, filtering/pagination, template editing, image insert, printer selection/DPI, single and batch RFID printing, and print-status sync.
- `PrintService` handles printer enumeration, per-label RFID tracking, sequential RFID data for multi-label prints, spooler status checks, and batch orchestration.
- `ZplGeneratorService` uses shared field resolution for visual output, raw RFID only for encoding, DPI-aware coordinates, native ZPL rotation, and inline `^GFA` image output.
- `TemplateService` persists print-affecting properties including `IsCentered`, rotation, and embedded image data.
- `BarcodeToImageConverter` uses `BarcodeStandard` and shared Code128 layout metrics, not the older synthetic random-bar preview.

## Review
- `dotnet run --project test_parse.csproj` initially failed inside the sandbox because MSBuild could not write a temp file under `BarTenderClone/obj`; rerun outside the sandbox passed with `Template parity probe passed.`
- `dotnet build testbartender.sln` passed with 0 errors.
- Current build warning: `NU1900` because the configured NuGet source `https://store.iotech.mn/v3/index.json` is unreachable for vulnerability metadata.
- I am ready to work directly on login/API host issues, resource parsing, price/status fields, designer/template behavior, preview-vs-print parity, ZPL generation, print/RFID behavior, and installer packaging.
- Treat installer artifact changes and OneDrive conflict-copy files carefully before any commit or packaging work.

---

# Rotated selection box and smooth resize

## Plan
- [x] Remove the active adorner-based resize overlay from the designer selection path.
- [x] Move selection border, 8 resize handles, and the top rotate marker into the rotated element frame.
- [x] Add rotation-aware resize math for 0/90/180/270 while preserving opposite edge/corner behavior.
- [x] Add always-on snap for move/resize to 1mm grid, template edges, safe margins, and centerlines.
- [x] Verify with the template parity probe and solution build.

## Notes
- Keep print/ZPL behavior unchanged; this pass only changes designer interaction.
- Keep rotation constrained to the existing `0/90/180/270` values.
- Rotate marker is visual only in this pass.

## Review
- Added `DesignerInteractionHelper` for rotated resize, movement clamping, and snap targets.
- Removed the old `ResizeAdorner` implementation from the active source; selection chrome is now rendered in the element template.
- The selected border, resize handles, and top rotate marker now share the same `RotateTransform` as the element content.
- After user testing showed the chrome still behaved like the content was upright, added a rotation-aware outer bounds converter so the `Thumb` layout/hit-test box swaps width/height for 90/270 degree elements and centers the rotated inner frame.
- Updated move/resize math so `X/Y` clamp/snap against the rotated visual bounds instead of the unrotated local width/height.
- `dotnet build testbartender.sln --no-restore /p:UseAppHost=false -v:minimal` passed with 0 errors.
- `dotnet build testbartender.sln --no-restore -v:minimal` passed with 0 errors after the running debug app lock was gone.
- `dotnet run --project test_parse.csproj --no-restore` passed before and after the final no-restore build.
- After the rotation-bounds correction, `dotnet build testbartender.sln --no-restore -v:minimal` passed with 0 errors and `dotnet run --project test_parse.csproj --no-restore` passed.
- Full restore/build attempts still report the existing `NU1900` warning for `https://store.iotech.mn/v3/index.json`; initial normal build was also blocked by sandbox `obj` permissions and then by a running `BizPro6Barcode.exe` lock before the no-restore verification path passed.

---

# Designer selection/resize regression fix

## Plan
- [x] Record the regression root cause and correction lesson.
- [x] Restore `LabelElement.X/Y/Width/Height` as unrotated design-box values for designer, template, and ZPL parity.
- [x] Fix zero-height text designer bounds so auto-populated code/name/price remain visible.
- [x] Rework rotated selection chrome and resize math so visual bounds are view-only and resize uses drag-start snapshots.
- [x] Verify with the template parity probe and solution build, then record results.

## Notes
- User report: after the rotated selection pass, only barcode remains visible and the blue selection box looks inaccurate.
- Root cause found in the current XAML: auto-populated text elements have `Height=0`, but the new root `Thumb.Height` uses `RotatedBoundsLengthConverter`, so those text elements collapse before their inner `TextBlock` can auto-size.
- Root cause found in the interaction helper: move/resize began treating `X/Y` as rotated visual bounds, which conflicts with the persisted/template/ZPL design-box coordinate contract.
- Added `DesignerElementGeometryConverter` backed by `DesignerInteractionHelper` so WPF layout uses a computed visual AABB while the model keeps unrotated design coordinates.
- Auto-populated text now gets explicit line heights; legacy/loaded zero-height text still gets a nonzero fallback designer box.
- Resize now records the drag-start geometry and applies cumulative deltas in local object axes, reducing per-frame drift while resizing rotated elements.

## Review
- `dotnet build testbartender.sln --no-restore -v:minimal` passed outside the sandbox with 0 errors. Sandbox build failed because MSBuild could not update WPF `obj` cache files.
- `dotnet run --project test_parse.csproj --no-restore` passed outside the sandbox with `Template parity probe passed.` Sandbox run failed because it could not update `obj\...\apphost.exe`.
- Existing warnings remain: `NU1900` for unreachable `https://store.iotech.mn/v3/index.json` vulnerability metadata and pre-existing nullable warnings in login/main/QR/resource paths.

## Follow-up Plan
- [x] Fix text clipping caused by scaled preview font using too-small explicit/fallback heights.
- [x] Make the rotated blue chrome use the same displayed local frame as text/barcode content.
- [x] Rebuild, rerun the parity probe, and relaunch the app for designer smoke testing.

## Follow-up Notes
- User follow-up screenshot shows product text clipped from the lower side and a rotated selected frame that does not feel accurate.
- `FontScaleConverter` renders text at `LabelSizeHelper.FONT_SCALING_FACTOR` (`1.5x`), but the previous text height calculation used unscaled font size, so the designer frame could be shorter than the rendered glyphs.
- `DesignerInteractionHelper.GetLocalSize(...)` now enforces a text display minimum based on the scaled preview font; explicit text heights that are too small no longer clip rendered glyphs in the designer.
- Auto-populated text line heights now use the same helper, so code/name/price and rotated selection chrome share the same displayed local frame.

## Follow-up Review
- `dotnet build testbartender.sln --no-restore -v:minimal` passed outside the sandbox with 0 errors.
- `dotnet run --project test_parse.csproj --no-restore` passed with `Template parity probe passed.`
- Restarted the debug app from `BarTenderClone\bin\Debug\net9.0-windows\BizPro6Barcode.exe`; latest process id is `55436`.
- Existing warnings remain: unreachable NuGet vulnerability metadata source `https://store.iotech.mn/v3/index.json` plus pre-existing nullable warnings.

## Rotated Handle Follow-up Plan
- [x] Move resize handles out of the rotated content grid and place them in the outer visual AABB by computed rotated handle points.
- [x] Keep each handle's `Tag` as the local object direction (`TopLeft`, `Top`, etc.) so resize math still uses the object's rotated local axes.
- [x] Reposition the visual rotate marker from computed rotated top-center instead of rotating it as a child of the content grid.
- [x] Rebuild, rerun the parity probe, and relaunch the app.

## Rotated Handle Follow-up Notes
- User follow-up: content now looks mostly correct, but the 8 resize points do not visually/physically follow the rotated object logic.
- Current XAML puts handle thumbs inside a rotated grid. That rotates visuals, but it also makes WPF layout/clipping and marker placement depend on the local grid, which is not precise enough for a designer surface.
- Added computed chrome points in `DesignerInteractionHelper` and routed them through `DesignerElementGeometryConverter`.
- The selection border still rotates with content, but resize thumbs are now unrotated controls positioned at the rotated frame's actual corner/edge-midpoint screen coordinates.

## Rotated Handle Follow-up Review
- `dotnet build testbartender.sln --no-restore -v:minimal` passed outside the sandbox with 0 errors.
- `dotnet run --project test_parse.csproj --no-restore` passed with `Template parity probe passed.`
- Restarted the debug app from `BarTenderClone\bin\Debug\net9.0-windows\BizPro6Barcode.exe`; latest process id is `47096`.
- Existing warnings remain: unreachable NuGet vulnerability metadata source and pre-existing nullable warnings.

---

# Production WYSIWYG raster print path

## Plan
- [x] Trace native ZPL generation and print-service configuration flow.
- [x] Add a default WYSIWYG raster render mode while keeping legacy native ZPL as a fallback.
- [x] Render the printable label layer into a full-label monochrome `^GFA` graphic and keep RFID encoding native.
- [x] Add fit behavior for auto-populated retail labels so code/name/price/barcode stay inside the label area.
- [x] Extend the regression probe, rebuild, rerun tests, and relaunch the app.

## Notes
- User goal: printed output should match exactly what is visible in the label designer area.
- Root issue: WPF designer and native ZPL text/barcode rendering use different engines, so exact WYSIWYG is not achievable with `^A`, `^BC`, and `^BQ` as the primary visual print path.
- Added `PrinterConfiguration.RenderMode`, defaulting to `WysiwygRaster`; `LegacyNativeZpl` remains available for troubleshooting/fallback.
- Added `LabelRasterRenderService`, which renders text/barcode/QR/image elements through WPF into a printer-DPI bitmap and emits a full-label `^GFA` graphic.
- `ZplGeneratorService` now emits the raster visual by default and appends RFID `^RFW` separately when RFID is enabled.
- Auto-populated retail layout now scales font sizes, spacing, and barcode height to fit the printable label height instead of relying on clipped text.

## Review
- `dotnet build testbartender.sln --no-restore -v:minimal` passed outside the sandbox with 0 errors.
- `dotnet run --project test_parse.csproj --no-restore` passed with `Template parity probe passed.`
- The regression probe now covers default raster mode (`^GFA` visual output, no native `^BC` visual command) and verifies native RFID encoding remains present.
- Restarted the debug app from `BarTenderClone\bin\Debug\net9.0-windows\BizPro6Barcode.exe`; latest process id is `32516`.
- Existing warnings remain: unreachable NuGet vulnerability metadata source plus pre-existing nullable warnings.
