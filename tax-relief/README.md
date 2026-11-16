# Tax Relief Helper (Malaysia, LHDN)

A mobile-first, tap-and-swipe helper for claiming Malaysian personal income tax reliefs for YA 2025 and YA 2026, with
optional AI receipt checking.

| File | What it is |
|---|---|
| `index.html` | The main app. Works offline. AI receipt checking is optional and uses your own Anthropic API key |
| `portal.html` | The same app built to run as a page on claude.ai, using your Claude account instead of an API key |

## Use it on your phone

**Option 1: host it (recommended).** In this GitHub repo, go to **Settings → Pages**, set the source to this branch and
the root folder, and save. Then open `https://<your-username>.github.io/WAZZUP/tax-relief/` on your phone and tap
**Share → Add to Home Screen**.

**Option 2: as a file.** Copy `index.html` to your phone (Files / AirDrop / Google Drive) and open it in Safari or Chrome.

## AI receipt checking

1. Create an API key at [console.anthropic.com](https://console.anthropic.com) → **API Keys**. Use a key just for this
   app and set a monthly spend limit on the account.
2. In the app, tap 📷, open **AI receipt checking**, paste the key and tap **Save key**.
3. Snap or upload a receipt. Claude (`claude-opus-5-5`) reads every line item and marks each one eligible, eligible
   only if conditions are met, or not eligible, with the relief it falls under. Tap **Add** to put the amount into
   your claim.

Notes:
- The key is stored only in that browser's local storage and is sent only to `api.anthropic.com`. Anyone who uses
  that browser on that device could read it, so don't use this on a shared device.
- Each check costs roughly a few sen on your Anthropic account (one image plus a short reply).
- Requests opt in to Anthropic's server-side refusal fallback (`fallbacks: "default"`), so a request Claude declines
  for safety reasons is retried on a fallback model.
- Without a key, or offline, the app still works fully. Receipts can be checked with the built-in keyword rules by
  typing the items.

## Accuracy

Relief limits are compiled from published guides on LHDN rules (Budget 2026 changes for YA 2026 included). Always
confirm against [hasil.gov.my](https://www.hasil.gov.my) before filing. This is not tax advice.
