# Lessons

- When the user says speed matters, stop expanding the review and move immediately to an implementation shortlist with only the changes that are safe to ship.
- When a preview-vs-print mismatch is production-critical, do not stop at heuristic parity; replace synthetic preview rendering with the real rendering library or shared layout math, then prove it with a build.
- When the user explicitly chooses an installer technology, use that toolchain directly and remove earlier alternative packaging changes before building or pushing.
- When restoring source from an earlier good commit to fix a regression, compare against the full recently shipped fix set before committing; do not let one fix silently remove another production fix.
- When fixing rotated designer selection, rotate the layout/hit-test bounding box as well as the visual chrome; a rotated inner transform alone can leave handles behaving as if the content is still upright.
- When building designer chrome, never bind the root hit-test or layout size directly to optional/zero content height; establish a nonzero design/display box first so legacy text elements cannot disappear.
- When the user requires exact designer-to-printer WYSIWYG, do not keep native printer text/barcode commands as the default visual output; use one render pipeline for preview and print, and keep native commands only for non-visual functions like RFID.
- When fixing text rotation, do not only protect zero-height text; explicit text heights can also be too small after rotation or font scaling, so measured rendered text bounds must be enforced for all text elements.
- When text still clips after height measurement, inspect width and glyph overhang too; narrow rotated Cyrillic text can be cut horizontally unless the local text box enforces measured unwrapped word width plus padding.
- When rotated text clips at both ends even after box growth, add an actual render inset; measuring extra padding alone does not help if the TextBlock still paints directly on the clipping edge.
- When the user says designer-to-print mismatch, do not overfit the explanation to orientation from photos; first separate orientation from WYSIWYG parity and focus on exact size/position matching.
- When rotated text still clips and only one middle handle seems to fix it, check whether wrapping is hiding the real width requirement; 90/270 label text must often fit as a single unwrapped run before rotation.
- When a fitted WPF `TextBlock` still clips after the math says it fits, stop relying on TextBlock's own desired-size behavior; render text through an explicit bounded Viewbox or custom drawing path so the visual box enforces the same contract as the measurement.
