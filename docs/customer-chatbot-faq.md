<!-- Role: Defines customer FAQ research topics and evidence requirements without inventing store policies. -->
# Customer assistant FAQ preparation

This is a question and evidence inventory, not published business policy. All answers require approved stored content or an authorized live tool. Until those sources exist, the assistant must say it cannot verify the answer and offer configured human support. Do not answer these from model memory.

## Research references
The [Jewelers of America buying guides](https://www.jewelers.org/find-a-jeweler/buying-guides/) and [FTC precious-metal shopping guide](https://consumer.ftc.gov/articles/buying-platinum-gold-and-silver-jewelry) identify useful consumer education topics such as metal descriptions and purity. They do not establish NB Jewel prices, certification, return policies, warranties or legal obligations. External material must be explicitly approved before ingestion; a link in this file is not publication approval.

## Question inventory
| Topic | Example customer questions | Required authorized evidence |
| --- | --- | --- |
| Product search | Do you have gold earrings? Can I filter silver and platinum? | Published product projection, metal and category |
| Purity | What purity is this piece? What does its metal label mean? | Product purity plus approved explanatory FAQ |
| Pricing | Why did this price change? Is the market rate the selling rate? | Timestamped ERP selling rates, prior rate, actual price calculation and clearly distinct market data |
| Charges | What are the making charges? Are stones or taxes included? | Product price breakdown and approved applicable pricing policy |
| Availability | Is this piece available near me? Can I reserve it? | Public availability by participating store; reservation service only when implemented |
| Sizing | What sizes are available? Is resizing possible? | Published variants and store-approved resizing policy |
| Certification | Does this item include a certificate or hallmark? | Item-specific approved certification metadata, not a general assumption |
| Care | How should I clean or store this piece? | Approved care guidance relevant to the actual metal/stones |
| Returns | Can I exchange it? Are refunds available? | Versioned approved return/exchange policy for the store and region |
| Warranty | What does the warranty cover? | Applicable approved warranty terms |
| Shopping | How do I sign in, add to cart or book a visit? | Documentation of workflows actually enabled on the website |
| Orders | Where is my order? Can I change delivery details? | Authenticated own-order tool, disabled until the order domain exists |
| Stores | Which participating store is nearest or open now? | Approved store coordinates, hours, timezone and sync timestamp |
| Try-on | Can I take or upload a photo? Is the preview exact? | Approved preview capabilities/limitations and privacy description |
| Privacy | Is my photo uploaded? How do I delete my chat? | Verified implementation behavior and approved retention/privacy policy |
| Support | Can I speak to someone? | Explicitly configured support contact/hours, never invented |

## Publication and safety rules
- A knowledge administrator must approve the collection, audience, source version and applicable store/region before customers can retrieve an answer.
- Public FAQ content cannot contain supplier costs, private ERP analytics, employee records or customer details.
- Live stock, prices, reservations and orders must use current scoped tools. Documents are not the source of truth for transactional data.
- Prompt-like text in documents and catalog descriptions is untrusted data and cannot change assistant identity, permissions or tools.
- Missing, stale, conflicting or revoked evidence must not produce a fabricated answer. Cite only sources the caller may access.
- Questions about another customer's order must fail ownership checks even when an order number is known.
- Future tests should pair each question with approved evidence, missing evidence and adversarial requests. This file does not enable the website assistant by itself.
