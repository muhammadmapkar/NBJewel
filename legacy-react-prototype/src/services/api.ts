import { CATEGORIES, generated, getFestivalDate, type Category, type Store } from '../data/generateData'
import {
  generatedKarigarData,
  KARIGARS,
  type DailyStoreVariance,
  type KarigarSummary,
  type MetalTransaction,
} from '../data/karigarData'
import { generatedInvoices, type Invoice } from '../data/invoiceData'
import { generatedAgedStock, type AgedSku } from '../data/agedStock'
import {
  generatedDesigns,
  cosineSimilarity,
  type DesignSku,
  type Motif,
  type Stone,
} from '../data/designData'
import {
  scanInvoices,
  type ComplianceException,
  type Severity,
} from '../rules/complianceRules'
import { defaultComplianceConfig, type ComplianceConfig } from '../config/complianceConfig'

export type Filters = { store: Store | 'All stores'; category: Category | 'All categories'; from?: string; to?: string }
export type RfmRow = { customerId: number; name: string; phone: string; goldScheme: boolean; recency: number; frequency: number; monetary: number; score: number; segment: string }
export type ForecastPoint = { date: string; actual?: number; forecast?: number }

const wait = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms))
const inFilters = (sale: (typeof generated.sales)[number], filters: Filters) =>
  (filters.store === 'All stores' || sale.store === filters.store) &&
  (filters.category === 'All categories' || sale.category === filters.category) &&
  (!filters.from || sale.date >= filters.from) && (!filters.to || sale.date <= filters.to)
const quintileScore = (value: number, values: number[], invert = false) => {
  const sorted = [...values].sort((a, b) => a - b)
  const rank = sorted.findIndex((v) => v >= value)
  const score = Math.min(5, Math.max(1, Math.ceil(((rank + 1) / sorted.length) * 5)))
  return invert ? 6 - score : score
}

const iso = (date: Date) => date.toISOString().slice(0, 10)

export async function getRfm(filters: Filters): Promise<RfmRow[]> {
  await wait(300)
  // Replace with fetch('/api/...') to connect real NB Jewel.
  const today = new Date('2026-09-28T00:00:00Z')
  const windowStart = new Date('2024-09-28T00:00:00Z')
  const relevant = generated.sales.filter((sale) => inFilters(sale, { ...filters, from: filters.from && filters.from > iso(windowStart) ? filters.from : iso(windowStart), to: filters.to && filters.to < iso(today) ? filters.to : iso(today) }))
  const metrics = generated.customers.map((customer) => {
    const customerSales = relevant.filter((sale) => sale.customerId === customer.id)
    const lastDate = customerSales.reduce((latest, sale) => sale.date > latest ? sale.date : latest, '')
    return { customer, recency: lastDate ? Math.max(0, Math.round((today.getTime() - new Date(lastDate).getTime()) / 86400000)) : 730, frequency: customerSales.length, monetary: customerSales.reduce((sum, sale) => sum + sale.value, 0) }
  })
  const recencies = metrics.map((m) => m.recency), frequencies = metrics.map((m) => m.frequency), monetary = metrics.map((m) => m.monetary)
  return metrics.map(({ customer, recency, frequency, monetary: spend }) => {
    const base = quintileScore(recency, recencies, true) + quintileScore(frequency, frequencies) + quintileScore(spend, monetary)
    const score = Math.min(15, base + (customer.goldScheme ? 1 : 0))
    const segment = spend === 0 ? 'Lost' : recency < 90 && score >= 12 ? 'Champions' : recency < 180 && score >= 9 ? 'Loyal' : spend > 300000 ? 'Big Spenders' : recency < 365 ? 'New' : 'At Risk'
    return { customerId: customer.id, name: customer.name, phone: customer.phone, goldScheme: customer.goldScheme, recency, frequency, monetary: spend, score, segment }
  })
}

export async function getForecast(filters: Filters, festivalName: string): Promise<{ festivalDate: string; series: ForecastPoint[]; categories: { category: Category; grams: number; pieces: number; value: number; stock: number; risk: 'red' | 'amber' | 'green' }[]; wape: number }> {
  await wait(350)
  // Replace with fetch('/api/...') to connect real NB Jewel.
  const selected = getFestivalDate(festivalName, 2026) ?? getFestivalDate(festivalName, 2025)!
  const festivalDate = new Date(selected.date)
  const previousDate = getFestivalDate(festivalName, 2025)?.date ?? selected.date
  const recent = generated.sales.filter((sale) => inFilters(sale, { ...filters, from: iso(new Date(new Date(previousDate).getTime() - 28 * 86400000)), to: iso(new Date(new Date(previousDate).getTime() + 28 * 86400000)) }))
  const priorDaily = new Map<string, number>()
  recent.forEach((sale) => priorDaily.set(sale.date, (priorDaily.get(sale.date) ?? 0) + sale.grams))
  const lastYearRate = generated.daily.filter((d) => d.date >= iso(new Date(new Date(previousDate).getTime() - 28 * 86400000)) && d.date <= iso(new Date(new Date(previousDate).getTime() + 28 * 86400000))).reduce((sum, d) => sum + d.goldRate, 0) / Math.max(1, priorDaily.size)
  const nowRate = generated.daily.filter((d) => d.date >= '2026-08-31').reduce((sum, d) => sum + d.goldRate, 0) / 29
  const goldAdjustment = Math.max(0.86, Math.min(1.08, lastYearRate / nowRate))
  const series: ForecastPoint[] = []
  for (let offset = -28; offset <= 28; offset++) {
    const prior = new Date(previousDate); prior.setUTCDate(prior.getUTCDate() + offset)
    const olderFestival = getFestivalDate(festivalName, 2024)
    const older = olderFestival ? new Date(olderFestival.date) : new Date(previousDate)
    older.setUTCDate(older.getUTCDate() + offset)
    const actualLastYear = generated.sales.filter((sale) => sale.date === iso(prior) && inFilters(sale, filters)).reduce((sum, sale) => sum + sale.grams, 0)
    const actualOlderYear = generated.sales.filter((sale) => sale.date === iso(older) && inFilters(sale, filters)).reduce((sum, sale) => sum + sale.grams, 0)
    const actual = (actualLastYear + actualOlderYear) / (olderFestival ? 2 : 1)
    const baseline = priorDaily.get(iso(prior)) ?? 30
    series.push({ date: `D${offset >= 0 ? '+' : ''}${offset}`, actual: Number(actual.toFixed(1)), forecast: Number((baseline * (1 + 0.06) * goldAdjustment).toFixed(1)) })
  }
  const categories = CATEGORIES.map((category, index) => {
    const rows = recent.filter((sale) => sale.category === category)
    const grams = rows.reduce((sum, sale) => sum + sale.grams, 0) * 1.06 * goldAdjustment
    const pieces = Math.round(rows.length * 1.06)
    const value = Math.round(rows.reduce((sum, sale) => sum + sale.value, 0) * 1.12)
    const stock = Math.round(grams * (0.62 + ((index * 13) % 20) / 100))
    return { category, grams: Math.round(grams), pieces, value, stock, risk: stock < grams * 0.55 ? 'red' as const : stock < grams * 0.8 ? 'amber' as const : 'green' as const }
  })
  const actualFestival = series.filter((p) => p.actual !== undefined)
  const wape = actualFestival.reduce((sum, point) => sum + Math.abs((point.actual ?? 0) - (point.forecast ?? 0)), 0) / Math.max(1, actualFestival.reduce((sum, point) => sum + (point.actual ?? 0), 0)) * 100
  return { festivalDate: selected.date, series, categories, wape: Math.round(wape * 10) / 10 }
}

// ==========================================
// TAB 3: FINE-GOLD RECONCILIATION API
// ==========================================

export interface GoldReconciliationFilters {
  store: Store | 'All stores'
  karigarId: string | 'All karigars'
  from?: string
  to?: string
}

export interface GoldReconciliationResponse {
  kpis: {
    fineGoldInStockG: number
    unexplainedVarianceG: number
    varianceValueAED: number
    worstStore: string
    currentGoldRateAED: number
  }
  ledger: DailyStoreVariance[]
  controlChart: {
    karigarId: string
    karigarName: string
    baselineMean: number
    ucl: number
    lcl: number
    jobs: {
      date: string
      jobId: string
      lossPct: number
      fineIssuedG: number
      fineReceivedG: number
      isAnomaly: boolean
    }[]
  }
  scorecard: KarigarSummary[]
  allKarigars: { id: string; name: string; store: Store }[]
}

export async function getGoldReconciliation(
  filters: GoldReconciliationFilters
): Promise<GoldReconciliationResponse> {
  await wait(300)
  // Replace with fetch('/api/...') to connect real NB Jewel.

  const ledger = generatedKarigarData.dailyLedger.filter((item) => {
    if (filters.store !== 'All stores' && item.store !== filters.store) return false
    if (filters.from && item.date < filters.from) return false
    if (filters.to && item.date > filters.to) return false
    return true
  })

  // Summary per store to find worst store
  const storeVarianceMap: Record<string, number> = {}
  ledger.forEach((l) => {
    storeVarianceMap[l.store] = (storeVarianceMap[l.store] || 0) + l.unexplainedVarianceG
  })
  let worstStore = 'Sharjah'
  let maxVar = -1
  Object.entries(storeVarianceMap).forEach(([st, v]) => {
    if (v > maxVar) {
      maxVar = v
      worstStore = st
    }
  })

  // Current stock estimation across stores
  const latestDates = ['Dubai Deira', 'Sharjah', 'Chennai T Nagar'].map((st) => {
    const rows = generatedKarigarData.dailyLedger.filter((l) => l.store === st)
    return rows[rows.length - 1]
  }).filter(Boolean)

  const fineGoldInStockG = Math.round(
    filters.store === 'All stores'
      ? latestDates.reduce((sum, row) => sum + row.actualClosingFineG, 0)
      : (latestDates.find((r) => r.store === filters.store)?.actualClosingFineG ?? 0)
  )

  const unexplainedVarianceG = Number(ledger.reduce((sum, l) => sum + l.unexplainedVarianceG, 0).toFixed(2))
  const varianceValueAED = Math.round(unexplainedVarianceG * generatedKarigarData.currentGoldRateAEDPerG)

  // Control Chart Data for selected or default karigar
  const targetKarigarId = filters.karigarId !== 'All karigars' ? filters.karigarId : 'K01' // defaults to Ramesh Verma
  const targetKarigar = KARIGARS.find((k) => k.id === targetKarigarId) || KARIGARS[0]
  const karigarSummary = generatedKarigarData.karigarSummaries.find((k) => k.id === targetKarigarId)!

  const jobs = generatedKarigarData.transactions
    .filter((t) => t.karigarId === targetKarigarId)
    .filter((t) => (!filters.from || t.date >= filters.from) && (!filters.to || t.date <= filters.to))
    .map((t) => ({
      date: t.date,
      jobId: t.jobId,
      lossPct: t.lossPct,
      fineIssuedG: t.fineIssuedG,
      fineReceivedG: t.fineReceivedG,
      isAnomaly: t.isAnomaly,
    }))

  const scorecard = generatedKarigarData.karigarSummaries.filter((k) => {
    if (filters.store !== 'All stores' && k.store !== filters.store) return false
    return true
  })

  return {
    kpis: {
      fineGoldInStockG,
      unexplainedVarianceG,
      varianceValueAED,
      worstStore,
      currentGoldRateAED: generatedKarigarData.currentGoldRateAEDPerG,
    },
    ledger,
    controlChart: {
      karigarId: targetKarigar.id,
      karigarName: targetKarigar.name,
      baselineMean: karigarSummary?.baselineLossPct ?? 3.0,
      ucl: karigarSummary?.uclPct ?? 3.8,
      lcl: karigarSummary?.lclPct ?? 2.2,
      jobs,
    },
    scorecard,
    allKarigars: KARIGARS,
  }
}

// ==========================================
// TAB 4: COMPLIANCE EXCEPTION ENGINE API
// ==========================================

export interface ComplianceFilters {
  country: 'All countries' | 'UAE' | 'India'
  ruleId: 'All rules' | string
  severity: 'All severities' | Severity
  store: Store | 'All stores'
}

export interface ComplianceScanResult {
  kpis: {
    scannedCount: number
    exceptionsCount: number
    highSeverityCount: number
    estimatedValueAtRiskAED: number
  }
  exceptions: ComplianceException[]
  summaryByRule: {
    ruleId: string
    ruleName: string
    severity: Severity
    country: 'UAE' | 'India'
    count: number
  }[]
  config: ComplianceConfig
}

export async function getComplianceExceptions(
  filters: ComplianceFilters,
  customConfig?: ComplianceConfig
): Promise<ComplianceScanResult> {
  await wait(300)
  // Replace with fetch('/api/...') to connect real NB Jewel.

  const config = customConfig || defaultComplianceConfig
  const allExceptions = scanInvoices(generatedInvoices, config)

  const filtered = allExceptions.filter((e) => {
    if (filters.country !== 'All countries' && e.country !== filters.country) return false
    if (filters.ruleId !== 'All rules' && e.ruleId !== filters.ruleId) return false
    if (filters.severity !== 'All severities' && e.severity !== filters.severity) return false
    if (filters.store !== 'All stores' && e.store !== filters.store) return false
    return true
  })

  // Estimated AED value at risk (convert INR to AED with 1 AED = ~22.8 INR)
  const totalValueAtRiskAED = Math.round(
    filtered.reduce((sum, e) => {
      const aed = e.currency === 'AED' ? e.valueAtRisk : e.valueAtRisk / 22.8
      return sum + aed
    }, 0)
  )

  const ruleMap: Record<string, { count: number; name: string; severity: Severity; country: 'UAE' | 'India' }> = {}
  allExceptions.forEach((e) => {
    if (!ruleMap[e.ruleId]) {
      ruleMap[e.ruleId] = { count: 0, name: e.ruleName, severity: e.severity, country: e.country }
    }
    ruleMap[e.ruleId].count++
  })

  const summaryByRule = Object.entries(ruleMap).map(([ruleId, info]) => ({
    ruleId,
    ruleName: info.name,
    severity: info.severity,
    country: info.country,
    count: info.count,
  }))

  return {
    kpis: {
      scannedCount: generatedInvoices.length,
      exceptionsCount: filtered.length,
      highSeverityCount: filtered.filter((e) => e.severity === 'High').length,
      estimatedValueAtRiskAED: totalValueAtRiskAED,
    },
    exceptions: filtered,
    summaryByRule,
    config,
  }
}

// ==========================================
// TAB 5: MELT, MARKDOWN, OR HOLD API
// ==========================================

export interface OptimizerParams {
  markdownPct: number // 0.05 to 0.40 (e.g. 0.20 for 20%)
  refiningLossPct: number // 0.005 to 0.05 (e.g. 0.02 for 2%)
  holdingCostPerMonthPct: number // 0.005 to 0.04 (e.g. 0.015 for 1.5%)
  goldRateAED: number // AED per gram (e.g. 312)
  categoryFilter?: Category | 'All categories'
  actionFilter?: 'All actions' | 'Melt' | 'Markdown' | 'Hold'
}

export interface OptimizedSkuItem extends AgedSku {
  meltValue: number
  markdownValue: number
  holdValue: number
  probSale60: number
  probSale90: number
  bestAction: 'Melt' | 'Markdown' | 'Hold'
  bestValue: number
  baselineValue: number
  netGainAED: number
}

export interface OptimizerResult {
  items: OptimizedSkuItem[]
  kpis: {
    totalItems: number
    meltCount: number
    meltTotalAED: number
    markdownCount: number
    markdownTotalAED: number
    holdCount: number
    holdTotalAED: number
    totalOpportunityAED: number
  }
  categoryImpact: {
    category: Category
    meltCount: number
    markdownCount: number
    holdCount: number
    totalGainAED: number
  }[]
}

export async function getAgedStockOptimization(params: OptimizerParams): Promise<OptimizerResult> {
  await wait(300)
  // Replace with fetch('/api/...') to connect real NB Jewel.

  const { markdownPct, refiningLossPct, holdingCostPerMonthPct, goldRateAED } = params

  const processed: OptimizedSkuItem[] = generatedAgedStock.map((sku) => {
    // 1. Melt value = fine weight x gold rate x (1 - refining loss %) minus remake cost
    const remakeCost = sku.grossWeightG * 12 // estimated remake / scrap recovery charge
    const meltValue = Math.max(0, Math.round(sku.fineWeightG * goldRateAED * (1 - refiningLossPct) - remakeCost))

    // 2. Markdown value = list price x (1 - markdown %) x probability of sale in 60 days
    const zMd = -0.5 + 2.5 * sku.recentSellThrough - 1.2 * (sku.ageDays / 365) + 3.0 * markdownPct
    const probSale60 = Number((1 / (1 + Math.exp(-zMd))).toFixed(3))
    const markdownValue = Math.round(sku.listPrice * (1 - markdownPct) * probSale60)

    // 3. Hold value = list price x probability of sale in 90 days minus holding cost
    const zHold = -0.8 + 2.0 * sku.recentSellThrough - 1.5 * (sku.ageDays / 365)
    const probSale90 = Number((1 / (1 + Math.exp(-zHold))).toFixed(3))
    const holdingCostTotal = sku.listPrice * holdingCostPerMonthPct * 3
    const holdValue = Math.max(0, Math.round(sku.listPrice * probSale90 - holdingCostTotal))

    // Baseline "doing nothing" value = passive hold liquidation
    const baselineValue = Math.max(0, Math.round(sku.cost * 0.72))

    let bestAction: 'Melt' | 'Markdown' | 'Hold' = 'Hold'
    let bestValue = holdValue

    if (meltValue >= markdownValue && meltValue >= holdValue) {
      bestAction = 'Melt'
      bestValue = meltValue
    } else if (markdownValue >= meltValue && markdownValue >= holdValue) {
      bestAction = 'Markdown'
      bestValue = markdownValue
    }

    const netGainAED = Math.max(0, Math.round(bestValue - baselineValue))

    return {
      ...sku,
      meltValue,
      markdownValue,
      holdValue,
      probSale60,
      probSale90,
      bestAction,
      bestValue,
      baselineValue,
      netGainAED,
    }
  })

  // Filter items
  const filtered = processed.filter((item) => {
    if (params.categoryFilter && params.categoryFilter !== 'All categories' && item.category !== params.categoryFilter) {
      return false
    }
    if (params.actionFilter && params.actionFilter !== 'All actions' && item.bestAction !== params.actionFilter) {
      return false
    }
    return true
  })

  // Sort descending by netGainAED
  filtered.sort((a, b) => b.netGainAED - a.netGainAED)

  let meltCount = 0, meltTotalAED = 0
  let markdownCount = 0, markdownTotalAED = 0
  let holdCount = 0, holdTotalAED = 0

  processed.forEach((item) => {
    if (item.bestAction === 'Melt') {
      meltCount++
      meltTotalAED += item.netGainAED
    } else if (item.bestAction === 'Markdown') {
      markdownCount++
      markdownTotalAED += item.netGainAED
    } else {
      holdCount++
      holdTotalAED += item.netGainAED
    }
  })

  // Category breakdown
  const categoryImpact = CATEGORIES.map((cat) => {
    const inCat = processed.filter((p) => p.category === cat)
    return {
      category: cat,
      meltCount: inCat.filter((p) => p.bestAction === 'Melt').length,
      markdownCount: inCat.filter((p) => p.bestAction === 'Markdown').length,
      holdCount: inCat.filter((p) => p.bestAction === 'Hold').length,
      totalGainAED: inCat.reduce((sum, p) => sum + p.netGainAED, 0),
    }
  })

  return {
    items: filtered,
    kpis: {
      totalItems: processed.length,
      meltCount,
      meltTotalAED,
      markdownCount,
      markdownTotalAED,
      holdCount,
      holdTotalAED,
      totalOpportunityAED: meltTotalAED + markdownTotalAED + holdTotalAED,
    },
    categoryImpact,
  }
}

// ==========================================
// TAB 6: DESIGN SIMILARITY SEARCH API
// ==========================================

export interface DesignDnaMetric {
  attribute: string
  salesCount: number
  avgSellThroughPct: number
}

export interface DesignSearchResult {
  allSkus: DesignSku[]
  selectedSku: DesignSku
  similarSkus: (DesignSku & { similarityPct: number })[]
  matchingSet: {
    earrings?: DesignSku & { similarityPct: number }
    bangles?: DesignSku & { similarityPct: number }
    rings?: DesignSku & { similarityPct: number }
  }
  dnaByMotif: DesignDnaMetric[]
  dnaByStone: DesignDnaMetric[]
  dnaByWeightBand: DesignDnaMetric[]
}

export async function getDesignSimilarity(skuId?: string): Promise<DesignSearchResult> {
  await wait(300)
  // Replace with fetch('/api/...') to connect real NB Jewel.

  const selectedSku = generatedDesigns.find((d) => d.sku === skuId) ?? generatedDesigns[0]

  // Compute cosine similarity against all other SKUs
  const scored = generatedDesigns
    .filter((d) => d.sku !== selectedSku.sku)
    .map((d) => ({
      ...d,
      similarityPct: Number((cosineSimilarity(selectedSku.embedding, d.embedding) * 100).toFixed(1)),
    }))
    .sort((a, b) => b.similarityPct - a.similarityPct)

  const top8 = scored.slice(0, 8)

  // Matching set suggestions
  // If selected piece is a necklace, look for similar earrings & bangles
  const findMatchingCategory = (category: Category) => {
    return scored
      .filter((d) => d.category === category)
      .slice(0, 1)[0]
  }

  const matchingSet = {
    earrings: findMatchingCategory('Earrings'),
    bangles: findMatchingCategory('Bangles'),
    rings: findMatchingCategory('Rings'),
  }

  // Design DNA breakdowns
  const motifMap: Record<string, { sales: number; totalSellThrough: number; count: number }> = {}
  const stoneMap: Record<string, { sales: number; totalSellThrough: number; count: number }> = {}
  const weightMap: Record<string, { sales: number; totalSellThrough: number; count: number }> = {}

  generatedDesigns.forEach((d) => {
    // Motif
    if (!motifMap[d.motif]) motifMap[d.motif] = { sales: 0, totalSellThrough: 0, count: 0 }
    motifMap[d.motif].sales += d.salesCount
    motifMap[d.motif].totalSellThrough += d.sellThroughPct
    motifMap[d.motif].count++

    // Stone
    if (!stoneMap[d.stone]) stoneMap[d.stone] = { sales: 0, totalSellThrough: 0, count: 0 }
    stoneMap[d.stone].sales += d.salesCount
    stoneMap[d.stone].totalSellThrough += d.sellThroughPct
    stoneMap[d.stone].count++

    // Weight band
    if (!weightMap[d.weightBand]) weightMap[d.weightBand] = { sales: 0, totalSellThrough: 0, count: 0 }
    weightMap[d.weightBand].sales += d.salesCount
    weightMap[d.weightBand].totalSellThrough += d.sellThroughPct
    weightMap[d.weightBand].count++
  })

  const dnaByMotif = Object.entries(motifMap).map(([attribute, val]) => ({
    attribute,
    salesCount: val.sales,
    avgSellThroughPct: Math.round(val.totalSellThrough / val.count),
  }))

  const dnaByStone = Object.entries(stoneMap).map(([attribute, val]) => ({
    attribute,
    salesCount: val.sales,
    avgSellThroughPct: Math.round(val.totalSellThrough / val.count),
  }))

  const dnaByWeightBand = Object.entries(weightMap).map(([attribute, val]) => ({
    attribute,
    salesCount: val.sales,
    avgSellThroughPct: Math.round(val.totalSellThrough / val.count),
  }))

  return {
    allSkus: generatedDesigns,
    selectedSku,
    similarSkus: top8,
    matchingSet,
    dnaByMotif,
    dnaByStone,
    dnaByWeightBand,
  }
}
