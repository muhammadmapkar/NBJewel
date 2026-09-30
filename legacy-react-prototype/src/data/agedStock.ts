import { CATEGORIES, type Category } from './generateData'
import { PURITY_FACTORS, type Purity } from './karigarData'

export interface AgedSku {
  sku: string
  category: Category
  purity: Purity
  grossWeightG: number
  fineWeightG: number
  ageDays: number
  listPrice: number // in AED
  cost: number // in AED
  makingLossPct: number // %
  motif: string
  stoneType: string
  weightBand: 'Light (<10g)' | 'Medium (10-30g)' | 'Heavy (>30g)'
  recentSellThrough: number // 0 to 1 scale
}

const MOTIFS = ['Floral', 'Temple', 'Geometric', 'Peacock', 'Filigree', 'Classic'] as const
const STONE_TYPES = ['None', 'Polki', 'Kundan', 'Ruby', 'Emerald', 'CZ'] as const

const makeRng = (seed: number) => {
  let state = seed >>> 0
  return () => {
    state = (1664525 * state + 1013904223) >>> 0
    return state / 4294967296
  }
}

export const generatedAgedStock: AgedSku[] = (() => {
  const rng = makeRng(20260931)
  const list: AgedSku[] = []
  const count = 400

  for (let i = 0; i < count; i++) {
    const category = CATEGORIES[Math.floor(rng() * CATEGORIES.length)]
    const purities: Purity[] = ['22K', '22K', '18K', '24K']
    const purity = purities[Math.floor(rng() * purities.length)]
    const factor = PURITY_FACTORS[purity]

    let weightBand: AgedSku['weightBand']
    let grossWeightG: number
    const bandRoll = rng()
    if (bandRoll < 0.3) {
      weightBand = 'Light (<10g)'
      grossWeightG = Number((3.5 + rng() * 6).toFixed(2))
    } else if (bandRoll < 0.75) {
      weightBand = 'Medium (10-30g)'
      grossWeightG = Number((10 + rng() * 19.5).toFixed(2))
    } else {
      weightBand = 'Heavy (>30g)'
      grossWeightG = Number((30 + rng() * 45).toFixed(2))
    }

    const fineWeightG = Number((grossWeightG * factor).toFixed(3))
    const ageDays = Math.round(90 + rng() * 630) // 90 to 720 days aged

    // Realistic cost structure based on gold content + craft
    const baseGoldCost = fineWeightG * 305
    const makingCost = grossWeightG * (25 + rng() * 35)
    const cost = Math.round(baseGoldCost + makingCost)
    const listPrice = Math.round(cost * (1.18 + rng() * 0.22)) // 18-40% markup

    const motif = MOTIFS[Math.floor(rng() * MOTIFS.length)]
    const stoneType = STONE_TYPES[Math.floor(rng() * STONE_TYPES.length)]
    const makingLossPct = Number((2.2 + rng() * 2.5).toFixed(2))

    // Recent sell-through depends on age and category
    // older items generally have lower sell-through
    const baseSellThrough = Math.max(0.02, Math.min(0.65, 0.45 - (ageDays / 1200) + (rng() - 0.5) * 0.2))
    const recentSellThrough = Number(baseSellThrough.toFixed(2))

    list.push({
      sku: `SKU-${7000 + i}`,
      category,
      purity,
      grossWeightG,
      fineWeightG,
      ageDays,
      listPrice,
      cost,
      makingLossPct,
      motif,
      stoneType,
      weightBand,
      recentSellThrough,
    })
  }

  return list
})()
