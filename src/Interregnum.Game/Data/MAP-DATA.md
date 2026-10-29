# Map data

`western-europe-land.geojson` is a reduced extract of Natural Earth's 1:50m
Admin 0 Countries dataset. Only geometry and source country codes needed to draw
the physical Western Europe context are retained. Modern political boundaries
are not rendered or used by the simulation.

Natural Earth vector data is in the public domain:

- Source: https://github.com/nvkelso/natural-earth-vector
- Terms: https://www.naturalearthdata.com/about/terms-of-use/
- Retrieved: 2026-10-25

`western-europe-terrain.png` is a 1920x1560 cropped and presentation-adjusted
extract of the 1:10m Natural Earth I with Shaded Relief, version 3.2.0. The
source combines satellite-derived land-cover color with shaded relief. The
extract covers 12 degrees west to 20 degrees east and 35 to 61 degrees north,
matching the map projection used by `ProvinceMap`. Saturation, contrast, and
sharpness were adjusted so physical detail and political overlays remain
legible together.

The raster's water color is not used to define land. Its alpha mask is generated
from `western-europe-land.geojson`, preserving the existing vector coastline and
water gaps such as the English Channel and Strait of Gibraltar. This avoids
coarse raster shoreline artifacts and keeps modern administrative boundaries
out of the presentation.

- Source: https://www.naturalearthdata.com/downloads/10m-raster-data/10m-natural-earth-1/
- Archive: `NE1_HR_LC_SR.zip`
- Version: 3.2.0
- Terms: https://www.naturalearthdata.com/about/terms-of-use/
- Retrieved: 2026-10-29

The interactive Gaul provinces remain fictionalized scenario geometry and must
not be interpreted as historical borders.

## M1.4 presentation layers

The terrain raster supplies physical relief and generalized vegetation/land
cover without political boundaries. The separate vector mask supplies the
continental contour. Ocean bands and river paths are drawn by `ProvinceMap`.
The Rhine, Rhone, Loire, Seine, and Garonne paths are
deliberately generalized, hand-authored presentation coordinates for
orientation at province scale. Neither the modern land-cover source nor those
paths are authoritative simulation terrain, reconstructed 480 AD land cover,
or survey-grade hydrography. Modern political boundaries are never drawn.

Political ownership, selection, at-risk status, rebellion, settlements, and
armies are separate overlays sourced from scenario geometry and the current
Core `WorldState`. The simulation never infers rules from map pixels or these
decorative paths.
