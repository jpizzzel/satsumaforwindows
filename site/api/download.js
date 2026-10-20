// GET /download (rewritten to /api/download) -> redirects to the newest Windows zip.
const { latestRelease, downloadUrl } = require("./_github");

module.exports = async (req, res) => {
  res.setHeader("Cache-Control", "no-store");
  try {
    const found = await latestRelease();
    // No release yet: send people back to the page, which explains it's on the way.
    if (!found) return res.redirect(302, "/?release=soon#download");
    res.redirect(302, await downloadUrl(found.asset));
  } catch (error) {
    console.error(error);
    res.status(502).setHeader("Content-Type", "text/plain; charset=utf-8");
    res.end("Couldn't get the download from GitHub right now. Please try again in a minute.");
  }
};
