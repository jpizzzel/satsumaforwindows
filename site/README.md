# Satsuma website

The landing page. It's plain HTML, CSS and JS with two tiny Vercel functions, so there's no build step.

```
index.html, styles.css, app.js   the page (the hero demo mirrors the real app's wheel)
api/latest.js                    newest version and zip size, for the page
api/download.js                  /download sends people straight to the newest zip
api/_github.js                   shared GitHub Releases helpers
og.png                           the picture link previews show
```

## Deploying on Vercel

1. In Vercel, add a new project from this repo.
2. Set **Root Directory** to `site`. Leave the framework preset on "Other" and the build command empty.
3. Add an environment variable called `GITHUB_TOKEN`:
   - On GitHub go to Settings, Developer settings, Personal access tokens, Fine-grained tokens, and make a new one.
   - Repository access: only `satsumaforwindows`.
   - Permissions: Contents, read-only. That's all it needs.
   - Paste it into Vercel as `GITHUB_TOKEN` and redeploy.
4. Optional: `GITHUB_REPO` if the releases ever move to a different repo (defaults to `jpizzzel/satsumaforwindows`).

Keep the token even though the repo is public. Without it GitHub only allows 60 requests an hour, shared by
everyone on Vercel's servers, so downloads could start failing on a busy day. With it the limit is 5,000.

## How the download works

`/download` looks up the newest release on GitHub and redirects to its `win-x64.zip`. For a private repo it asks
GitHub for a short-lived signed link, so visitors can download without seeing the repo and the token never leaves
the server. If there's no release yet, it sends people back to the page, which says the first release is on its way.

## Trying it locally

`npx vercel dev` from this folder runs the page and the functions together. Opening `index.html` straight from
disk works too; the demo runs, and the download info just stays on its defaults.
