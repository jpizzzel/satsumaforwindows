// Shared bits for talking to GitHub Releases. Vercel doesn't turn files starting with "_" into endpoints.
//
// Environment variables (set them in the Vercel project settings):
//   GITHUB_REPO   owner/name of the repo with the releases. Defaults to jpizzzel/satsumaforwindows.
//   GITHUB_TOKEN  recommended. Required for a private repo, and for a public one it lifts GitHub's limit from
//                 60 requests an hour (shared across Vercel's servers) to 5,000. A fine-grained token with
//                 read-only "Contents" access to just that repo is enough.

const REPO = process.env.GITHUB_REPO || "jpizzzel/satsumaforwindows";

function headers(extra = {}) {
  const h = {
    "Accept": "application/vnd.github+json",
    "User-Agent": "satsuma-site",
    "X-GitHub-Api-Version": "2022-11-28",
    ...extra,
  };
  if (process.env.GITHUB_TOKEN) h.Authorization = `Bearer ${process.env.GITHUB_TOKEN}`;
  return h;
}

// The newest published release and its Windows zip, or null if there isn't one yet.
async function latestRelease() {
  const res = await fetch(`https://api.github.com/repos/${REPO}/releases/latest`, { headers: headers() });
  if (res.status === 404) return null;
  if (!res.ok) throw new Error(`GitHub answered ${res.status} for the latest release`);
  const release = await res.json();
  const asset = (release.assets || []).find((a) => /win-x64\.zip$/i.test(a.name));
  return asset ? { release, asset } : null;
}

// A link the visitor's browser can download from. For a public repo that's just the normal download URL.
// For a private one, GitHub hands the API a short-lived signed link instead, which works for anyone
// for a few minutes, so the token itself never leaves the server.
async function downloadUrl(asset) {
  if (!process.env.GITHUB_TOKEN) return asset.browser_download_url;
  const res = await fetch(asset.url, {
    headers: headers({ Accept: "application/octet-stream" }),
    redirect: "manual",
  });
  const location = res.headers.get("location");
  if (res.body) await res.body.cancel().catch(() => {});
  if (res.status >= 300 && res.status < 400 && location) return location;
  throw new Error(`GitHub answered ${res.status} instead of a download link`);
}

module.exports = { latestRelease, downloadUrl, REPO };
