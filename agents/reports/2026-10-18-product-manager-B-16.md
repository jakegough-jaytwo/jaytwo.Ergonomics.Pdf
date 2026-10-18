# B-16 acceptance

## Task and owner

Product Manager. Accepted B-16 as the Poppler checks that were added. Edited `agents/state/backlog.md` only. G6 is not passed.

## Verified facts

`agents/reports/2026-10-18-qa-engineer-B-16.md` did not sign off. The center `Align` word only had to sit more than 80 points from its neighbors. Poppler 26.09.0 established the other listed properties with `pdftotext` and `pdftoppm`. No second renderer was added.

`agents/reports/2026-10-18-qa-engineer-B-16-alignment.md` signed off the correction. The three `Align` words, ordered by XMin, must fall in 30–60, 174–180, and 280–340. `AppearanceRenderTests` passed 2/2 on `net8.0` and on `net6.0`.

The Technical Lead approved that corrected test in `agents/reports/2026-10-18-tech-lead-B-16-review.md`. The center assertion is `Assert.InRange(aligned[1].XMin, 174, 180)`. That review does not pass G6.

## Handoff

B-16 is closed as those Poppler checks. G6 stays unpassed. There is no second renderer, and the gate's acceptance is wider than this set.
