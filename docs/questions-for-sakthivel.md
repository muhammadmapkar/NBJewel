# Questions for Sakthivel

These are open questions that change how the real version should be built. The demo currently assumes the defaults given here, and every default is an assumption, not a fact.

| # | Question | Why it matters | What the demo assumes |
|---|---|---|---|
| 1 | Is NB Jewel's system Business Central, or a .NET application on SQL Server? | Decides the repository layer: SQL Server adapter or Business Central API adapter. See the swap plan in `architecture.md`. | Assumed: either can be placed behind `IItemRepository`. |
| 2 | Is an item a design (one catalog entry for many pieces) or a tagged piece (one physical item with its own tag)? | Changes what "item", "stock" and "variant" mean in every screen and tool. | Assumed: an item has a design code, and stock is counted per item per store. |
| 3 | What are the real field names and tables for items, stock, stores, categories and rates? | The sample field names are invented. | Assumed: the sample names (code, netWt, makingType and so on). |
| 4 | Which making charge types exist, and how is each calculated and rounded? | The price breakdown depends on it. | Assumed: per gram, percent of metal value, and flat amount, rounded to 2 decimals. |
| 5 | Where does the metal rate come from, how often does it change, and which one is the "latest"? | The rate tool and every price depend on it. | Assumed: one rate per metal and purity, in AED, newest effective date wins. |
| 6 | What are the HUID rules (who assigns it, is it per piece, is it required)? | Affects the item form validation and search. | Assumed: HUID is optional text on an item. |
| 7 | Where are images stored, and how are they served and uploaded? | The demo has fake images and no upload storage. | Assumed: images have a URL and one primary image. |
| 8 | Do items have variants such as size and color? How are they stocked and priced? | The demo variants and price delta are invented. | Assumed: variants have size, color, a price delta and their own stock. |
| 9 | Which AI vendors and models are approved, and in which regions? | Decides whether Gemini can be used at all. See `data-privacy-checklist.md`. | Assumed: Gemini is fine for the demo with fake data only. |
| 10 | React or Angular for the real front end? | The demo front end is Angular. A React prototype from earlier work is kept in `legacy-react-prototype/`. | Assumed: Angular. |
| 11 | What is the demo date, and who will attend? | Sets the deadline and how much polish is needed. | Not set. |
| 12 | How do users sign in, and should the assistant see different data for different users? | Authentication and permissions are not built. | Assumed: no login, one set of data for everyone. |
| 13 | Is the "master chit" the design master? Is one design shared by many items? | Decides the design table and the dropdown. | Assumed: yes. A design has an id, a code and a name, and many items can share it. |
| 14 | What are the real item statuses, and may a user move a status freely (any-to-any), or only in some order? | The demo uses Draft, With Smith, Received, Complete. What happens to old Active and Discontinued items is not known. | Assumed: the four statuses above, any-to-any, with confirmation on a backward move. Active and Discontinued items were remapped to Complete. |
| 15 | Should stock entry add to on-hand or replace it? Is 0 allowed? | Changes the stock form and audit needs. | Assumed: it adds, and the quantity must be above 0. |
| 16 | Does the real schema have color, price delta and reserved stock? | The SQL repository returns empty or zero values for them. | Assumed: not in the schema. |
| 17 | What counts as low stock? Per variant and store, or per item? | Decides the Needs attention card and any alert. | Assumed: fewer than 3 units available (on hand minus reserved) in one variant and store. |
