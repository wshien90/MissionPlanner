# FSEMI brand assets

From the FPT Semiconductor website, https://fpt-semiconductor.com/ (October 2026):

| File | Source |
|---|---|
| `logo-on-light.svg` | header logo, `wp-content/uploads/2026/03/Isolation_Mode.svg` (wordmark in #003699) |
| `logo-on-dark.svg` | header logo for dark backgrounds, `wp-content/uploads/2026/04/Isolation_Mode-2.svg` (white wordmark) |
| `mark-512.png` | site icon, `wp-content/uploads/2026/03/Frame-2144772366.png` |

Colours:

| Colour | Use on the site | Use in FSEMI Mission Planner |
|---|---|---|
| `#010837` | page loader and footer background (navy) | main background, button text, splash |
| `#003699` | `--wp--preset--color--primary`, logo wordmark | banners |
| `#2E65A5` | logo, F (blue) | progress bars, button outline |
| `#DB773A` | logo, P (orange); `--wp--preset--color--secondary` | splash accent |
| `#50A657` | logo, T (green) | splash accent |
| `#6388F7` | most used accent blue | buttons, highlights, menu icons |

Made from them, in `fsemi/` and copied next to the exe by the build:

- `fsemi.ico` (16-256 px) and `icon.png` (128 px): the FPT mark.
- `logo2.png` (200 × 31): the white wordmark at the right of the menu bar; clicking it opens the website.
- `splashbg.png` (600 × 375): the start-up screen.

`make_brand_assets.js` renders `logo2.png` and `splashbg.png`; the icons are the site icon resized.
The theme is `FSEMI.mpsystheme` at the top of the repository.
