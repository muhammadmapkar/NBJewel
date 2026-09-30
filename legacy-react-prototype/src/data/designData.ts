import { CATEGORIES, STORES, type Category, type Store } from './generateData'

export type MetalColour = 'Yellow Gold' | 'Rose Gold' | 'White Gold' | 'Two-Tone'
export type Motif = 'Floral' | 'Temple' | 'Geometric' | 'Peacock' | 'Filigree' | 'Classic'
export type Stone = 'None' | 'Polki' | 'Kundan' | 'Ruby' | 'Emerald' | 'CZ'
export type WeightBand = 'Light (<10g)' | 'Medium (10-30g)' | 'Heavy (>30g)'

export interface DesignSku {
  sku: string
  title: string
  category: Category
  motif: Motif
  stone: Stone
  metalColour: MetalColour
  weightBand: WeightBand
  store: Store
  grossWeightG: number
  priceAED: number
  salesCount: number
  sellThroughPct: number // %
  embedding: number[]
}

export const MOTIFS: Motif[] = ['Floral', 'Temple', 'Geometric', 'Peacock', 'Filigree', 'Classic']
export const STONES: Stone[] = ['None', 'Polki', 'Kundan', 'Ruby', 'Emerald', 'CZ']
export const METAL_COLOURS: MetalColour[] = ['Yellow Gold', 'Rose Gold', 'White Gold', 'Two-Tone']
export const WEIGHT_BANDS: WeightBand[] = ['Light (<10g)', 'Medium (10-30g)', 'Heavy (>30g)']

const makeRng = (seed: number) => {
  let state = seed >>> 0
  return () => {
    state = (1664525 * state + 1013904223) >>> 0
    return state / 4294967296
  }
}

// Helper to calculate cosine similarity between two numeric vectors
export function cosineSimilarity(vecA: number[], vecB: number[]): number {
  let dotProduct = 0
  let normA = 0
  let normB = 0
  for (let i = 0; i < vecA.length; i++) {
    dotProduct += vecA[i] * vecB[i]
    normA += vecA[i] * vecA[i]
    normB += vecB[i] * vecB[i]
  }
  if (normA === 0 || normB === 0) return 0
  return dotProduct / (Math.sqrt(normA) * Math.sqrt(normB))
}

export const generatedDesigns: DesignSku[] = (() => {
  const rng = makeRng(20261001)
  const list: DesignSku[] = []
  const count = 300

  // Category vector offsets to influence category clustering
  const categoryOffset: Record<Category, number[]> = {
    'Rings': [1.0, 0.2, -0.4, 0.1],
    'Necklaces': [-0.3, 1.2, 0.5, -0.2],
    'Bangles': [0.4, -0.3, 1.1, 0.6],
    'Earrings': [0.8, 0.5, -0.6, 0.2],
    'Coins & Bars': [-0.8, -0.9, -0.7, 1.3],
  }

  // Motif vector offsets
  const motifOffset: Record<Motif, number[]> = {
    'Floral': [0.6, 0.8, 0.2, -0.1],
    'Temple': [0.9, -0.4, 0.7, 0.3],
    'Geometric': [-0.5, 0.7, -0.8, 0.4],
    'Peacock': [0.8, 0.3, 0.9, -0.4],
    'Filigree': [0.3, 0.9, -0.2, 0.7],
    'Classic': [0.1, -0.2, 0.4, -0.5],
  }

  // Stone vector offsets
  const stoneOffset: Record<Stone, number[]> = {
    'None': [-0.6, -0.5, 0.1, 0.2],
    'Polki': [0.8, 0.4, 0.9, 0.1],
    'Kundan': [0.7, 0.6, 0.8, -0.2],
    'Ruby': [0.5, 0.9, 0.3, 0.4],
    'Emerald': [0.4, 0.8, 0.5, -0.6],
    'CZ': [-0.3, 0.2, -0.7, 0.8],
  }

  for (let i = 0; i < count; i++) {
    const category = CATEGORIES[Math.floor(rng() * CATEGORIES.length)]
    const motif = MOTIFS[Math.floor(rng() * MOTIFS.length)]
    const stone = category === 'Coins & Bars' ? 'None' : STONES[Math.floor(rng() * STONES.length)]
    const metalColour = METAL_COLOURS[Math.floor(rng() * METAL_COLOURS.length)]
    const weightBand = WEIGHT_BANDS[Math.floor(rng() * WEIGHT_BANDS.length)]
    const store = STORES[Math.floor(rng() * STORES.length)]

    let grossWeightG = 0
    if (weightBand === 'Light (<10g)') grossWeightG = Number((2.8 + rng() * 6.5).toFixed(2))
    else if (weightBand === 'Medium (10-30g)') grossWeightG = Number((11 + rng() * 18).toFixed(2))
    else grossWeightG = Number((31 + rng() * 48).toFixed(2))

    const priceAED = Math.round(grossWeightG * 315 * (stone === 'None' ? 1.15 : 1.45) * (metalColour === 'Two-Tone' ? 1.08 : 1.0))
    const salesCount = Math.floor(rng() * 42)
    const sellThroughPct = Math.round(22 + rng() * 68)

    // Build 16-dimensional normalized vector
    const catV = categoryOffset[category]
    const motV = motifOffset[motif]
    const stnV = stoneOffset[stone]

    const rawEmbedding: number[] = [
      catV[0] * 1.5 + (rng() - 0.5) * 0.2,
      catV[1] * 1.5 + (rng() - 0.5) * 0.2,
      catV[2] * 1.5 + (rng() - 0.5) * 0.2,
      catV[3] * 1.5 + (rng() - 0.5) * 0.2,

      motV[0] * 1.4 + (rng() - 0.5) * 0.2,
      motV[1] * 1.4 + (rng() - 0.5) * 0.2,
      motV[2] * 1.4 + (rng() - 0.5) * 0.2,
      motV[3] * 1.4 + (rng() - 0.5) * 0.2,

      stnV[0] * 1.3 + (rng() - 0.5) * 0.2,
      stnV[1] * 1.3 + (rng() - 0.5) * 0.2,
      stnV[2] * 1.3 + (rng() - 0.5) * 0.2,
      stnV[3] * 1.3 + (rng() - 0.5) * 0.2,

      metalColour === 'Yellow Gold' ? 1.0 : metalColour === 'Rose Gold' ? 0.3 : metalColour === 'White Gold' ? -0.8 : 0.6,
      weightBand === 'Light (<10g)' ? -0.7 : weightBand === 'Medium (10-30g)' ? 0.1 : 0.9,
      (rng() - 0.5) * 0.4,
      (rng() - 0.5) * 0.4,
    ]

    // Normalize embedding vector to unit length
    const norm = Math.sqrt(rawEmbedding.reduce((sum, v) => sum + v * v, 0))
    const embedding = rawEmbedding.map((v) => Number((v / (norm || 1)).toFixed(5)))

    const title = `${metalColour} ${motif} ${stone !== 'None' ? stone + ' ' : ''}${category.slice(0, -1)}`

    list.push({
      sku: `DSG-${3000 + i}`,
      title,
      category,
      motif,
      stone,
      metalColour,
      weightBand,
      store,
      grossWeightG,
      priceAED,
      salesCount,
      sellThroughPct,
      embedding,
    })
  }

  return list
})()
