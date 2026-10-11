# Data dictionary placeholder

The JSON files in `sample-data/` define the initial fake fixture shapes.

- Weights use numeric grams with three decimal places.
- `parentId` links a category to its parent and is `null` for root categories.
- `stock.json` contains one row for each item, variant and store with stock. `variantId` is left out for items that have no variants.
- `designs.json` is the design master (assumed to be the "master chit"): `designId`, `designCode`, `name`. 10 sample designs.
- `variants.json` lists size and color variants for 20 of the 30 items. Every third item has none.
- `items.json` has `designId` (the stored link) and `designCode` (copied from the design for display).
- Item `status` is one of Draft, With Smith, Received, Complete (assumed). It can only move forward.
- `metal-rates.json` contains synthetic per-gram values for test grounding.
- `huid` and `imageUrl` are fake placeholders and are not production values.
