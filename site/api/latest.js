// GET /api/latest -> { available, version, size, published, name }
// The page uses this to show the current version and download size.
const { latestRelease } = require("./_github");

module.exports = async (req, res) => {
  try {
    const found = await latestRelease();
    // Releases don't change often, so let Vercel's edge cache it for a few minutes.
    res.setHeader("Cache-Control", "public, s-maxage=300, stale-while-revalidate=3600");
    if (!found) return res.status(404).json({ available: false });
    const { release, asset } = found;
    res.status(200).json({
      available: true,
      version: release.tag_name,
      published: release.published_at,
      size: asset.size,
      name: asset.name,
    });
  } catch (error) {
    console.error(error);
    res.setHeader("Cache-Control", "no-store");
    res.status(502).json({ available: false, error: "Couldn't reach GitHub right now." });
  }
};
