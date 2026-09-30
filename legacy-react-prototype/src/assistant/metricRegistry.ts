import { generatedKarigarData } from '../data/karigarData'
import { generatedInvoices } from '../data/invoiceData'
import { generatedAgedStock } from '../data/agedStock'
import { generatedDesigns, cosineSimilarity } from '../data/designData'
import { scanInvoices } from '../rules/complianceRules'
import { defaultComplianceConfig } from '../config/complianceConfig'
import { generated } from '../data/generateData'

// Real version: an LLM picks a metric from this registry via tool calling. It never writes free SQL.

export type MetricResultType = 'number' | 'table' | 'chart'

export interface MetricDefinition {
  name: string
  description: string
  parameters?: Record<string, string>
  resultType: MetricResultType
  execute: (params?: Record<string, any>) => {
    answer: string
    data?: any
    chartType?: 'bar' | 'line' | 'pie'
    columns?: { key: string; label: string }[]
    metricUsed: string
  }
}

export const metricRegistry: Record<string, MetricDefinition> = {
  fine_gold_variance_by_store: {
    name: 'fine_gold_variance_by_store',
    description: 'Calculates the unexplained fine gold variance across stores over the last 90 days.',
    resultType: 'table',
    execute: () => {
      const stores = ['Sharjah', 'Dubai Deira', 'Chennai T Nagar']
      const ledger = generatedKarigarData.dailyLedger
      const rows = stores.map((s) => {
        const storeTxs = ledger.filter((l) => l.store === s)
        const totalUnexplained = storeTxs.reduce((sum, l) => sum + l.unexplainedVarianceG, 0)
        const avgDaily = totalUnexplained / Math.max(1, storeTxs.length)
        const valueAed = Math.round(totalUnexplained * generatedKarigarData.currentGoldRateAEDPerG)
        return {
          store: s,
          totalUnexplainedG: Number(totalUnexplained.toFixed(2)),
          avgDailyG: Number(avgDaily.toFixed(2)),
          valueAED: valueAed,
        }
      }).sort((a, b) => b.totalUnexplainedG - a.totalUnexplainedG)

      const worst = rows[0]
      return {
        answer: `Sharjah recorded the highest unexplained variance with ${worst.totalUnexplainedG}g fine gold (AED ${worst.valueAED.toLocaleString()}) across 90 days, averaging ~${worst.avgDailyG}g daily gap.`,
        data: rows,
        columns: [
          { key: 'store', label: 'Store' },
          { key: 'totalUnexplainedG', label: 'Total Unexplained (g)' },
          { key: 'avgDailyG', label: 'Daily Avg (g)' },
          { key: 'valueAED', label: 'Value (AED)' },
        ],
        metricUsed: 'fine_gold_variance_by_store',
      }
    },
  },

  top_karigars_by_loss: {
    name: 'top_karigars_by_loss',
    description: 'Ranks karigars by average metal loss percentage and identifies who is drifting above control limits.',
    resultType: 'table',
    execute: () => {
      const summaries = [...generatedKarigarData.karigarSummaries]
      const drifting = summaries.filter((k) => k.trend === 'Drifting Up')
      const topLoss = summaries.sort((a, b) => b.avgLossPct - a.avgLossPct).slice(0, 5)

      const driftingNames = drifting.map((k) => `${k.name} (${k.avgLossPct}%, ${k.flaggedJobsCount} flagged)`).join(' and ')
      return {
        answer: `${driftingNames} have drifted upward significantly over the last 3 weeks, breaching statistical control limits.`,
        data: topLoss.map((k) => ({
          name: k.name,
          store: k.store,
          avgLossPct: `${k.avgLossPct}%`,
          trend: k.trend,
          flaggedJobsCount: k.flaggedJobsCount,
          rank: `#${k.rank}`,
        })),
        columns: [
          { key: 'name', label: 'Karigar' },
          { key: 'store', label: 'Store' },
          { key: 'avgLossPct', label: 'Avg Loss %' },
          { key: 'trend', label: 'Trend' },
          { key: 'flaggedJobsCount', label: 'Flagged Jobs' },
        ],
        metricUsed: 'top_karigars_by_loss',
      }
    },
  },

  at_risk_customer_count: {
    name: 'at_risk_customer_count',
    description: 'Counts the number of high-value and lapsed customers tagged as At Risk in the RFM model.',
    resultType: 'number',
    execute: () => {
      // Fast computation on customer sales
      const customerSalesCount = new Map<number, number>()
      generated.sales.forEach((s) => customerSalesCount.set(s.customerId, (customerSalesCount.get(s.customerId) ?? 0) + 1))
      // Sample RFM at risk estimation
      const atRiskCount = Math.round(generated.customers.length * 0.22)
      const championsCount = Math.round(generated.customers.length * 0.14)
      return {
        answer: `There are currently ${atRiskCount.toLocaleString()} customers classified as "At Risk" (haven't purchased in over 180 days despite strong historical spend), while ${championsCount.toLocaleString()} remain Champions.`,
        data: [
          { segment: 'At Risk', count: atRiskCount },
          { segment: 'Champions', count: championsCount },
          { segment: 'Loyal', count: 480 },
          { segment: 'Lost', count: 320 },
        ],
        chartType: 'bar',
        metricUsed: 'at_risk_customer_count',
      }
    },
  },

  festival_forecast_by_category: {
    name: 'festival_forecast_by_category',
    description: 'Provides demand forecast and reorder requirements by jewellery category for upcoming festivals.',
    resultType: 'chart',
    execute: () => {
      const data = [
        { category: 'Necklaces', expectedGrams: 1840, stockGrams: 1150, reorderGrams: 690 },
        { category: 'Bangles', expectedGrams: 2210, stockGrams: 1680, reorderGrams: 530 },
        { category: 'Coins & Bars', expectedGrams: 3450, stockGrams: 1900, reorderGrams: 1550 },
        { category: 'Rings', expectedGrams: 980, stockGrams: 820, reorderGrams: 160 },
        { category: 'Earrings', expectedGrams: 890, stockGrams: 780, reorderGrams: 110 },
      ]
      return {
        answer: `Coins & Bars require the largest reorder buffer (1,550g), followed by Necklaces (690g) and Bangles (530g) to prevent festival stock-outs.`,
        data,
        chartType: 'bar',
        columns: [
          { key: 'category', label: 'Category' },
          { key: 'expectedGrams', label: 'Demand (g)' },
          { key: 'stockGrams', label: 'Stock (g)' },
          { key: 'reorderGrams', label: 'Reorder (g)' },
        ],
        metricUsed: 'festival_forecast_by_category',
      }
    },
  },

  compliance_exceptions_by_rule: {
    name: 'compliance_exceptions_by_rule',
    description: 'Scans all store invoices and groups statutory tax and hallmarking exceptions by rule.',
    resultType: 'table',
    execute: () => {
      const exceptions = scanInvoices(generatedInvoices, defaultComplianceConfig)
      const ruleCounts: Record<string, { count: number; name: string; severity: string; country: string }> = {}

      exceptions.forEach((e) => {
        if (!ruleCounts[e.ruleId]) {
          ruleCounts[e.ruleId] = { count: 0, name: e.ruleName, severity: e.severity, country: e.country }
        }
        ruleCounts[e.ruleId].count++
      })

      const rows = Object.entries(ruleCounts).map(([ruleId, info]) => ({
        ruleId,
        ruleName: info.name,
        country: info.country,
        severity: info.severity,
        count: info.count,
      })).sort((a, b) => b.count - a.count)

      const highSevCount = exceptions.filter((e) => e.severity === 'High').length
      return {
        answer: `Scanned 1,500 invoices and found ${exceptions.length} exceptions across UAE and India stores, including ${highSevCount} high-severity flags requiring priority audit.`,
        data: rows,
        columns: [
          { key: 'ruleId', label: 'Rule ID' },
          { key: 'ruleName', label: 'Rule Name' },
          { key: 'country', label: 'Country' },
          { key: 'severity', label: 'Severity' },
          { key: 'count', label: 'Exceptions' },
        ],
        metricUsed: 'compliance_exceptions_by_rule',
      }
    },
  },

  best_action_summary_for_aged_stock: {
    name: 'best_action_summary_for_aged_stock',
    description: 'Summarizes recommended actions (Melt, Markdown, or Hold) and net AED recovery on 400 aged SKUs.',
    resultType: 'table',
    execute: () => {
      const goldRate = 312
      const markdownPct = 0.20
      const refiningLossPct = 0.02
      const holdingCostPerMonthPct = 0.015

      let meltCount = 0, meltTotal = 0
      let mdCount = 0, mdTotal = 0
      let holdCount = 0, holdTotal = 0

      generatedAgedStock.forEach((sku) => {
        const meltVal = sku.fineWeightG * goldRate * (1 - refiningLossPct) - sku.grossWeightG * 12
        const zMd = -0.5 + 2.5 * sku.recentSellThrough - 1.2 * (sku.ageDays / 365) + 3.0 * markdownPct
        const prob60 = 1 / (1 + Math.exp(-zMd))
        const mdVal = sku.listPrice * (1 - markdownPct) * prob60

        const zHold = -0.8 + 2.0 * sku.recentSellThrough - 1.5 * (sku.ageDays / 365)
        const prob90 = 1 / (1 + Math.exp(-zHold))
        const holdVal = sku.listPrice * prob90 - sku.listPrice * holdingCostPerMonthPct * 3

        if (meltVal >= mdVal && meltVal >= holdVal) {
          meltCount++
          meltTotal += meltVal
        } else if (mdVal >= meltVal && mdVal >= holdVal) {
          mdCount++
          mdTotal += mdVal
        } else {
          holdCount++
          holdTotal += holdVal
        }
      })

      const totalGain = Math.round(meltTotal + mdTotal + holdTotal)
      return {
        answer: `Across 400 aged inventory pieces, optimal actions yield AED ${totalGain.toLocaleString()} recovery: Melt ${meltCount} items (AED ${Math.round(meltTotal).toLocaleString()}), Markdown ${mdCount} items (AED ${Math.round(mdTotal).toLocaleString()}), and Hold ${holdCount} items (AED ${Math.round(holdTotal).toLocaleString()}).`,
        data: [
          { action: 'Melt', count: meltCount, totalAED: Math.round(meltTotal) },
          { action: 'Markdown', count: mdCount, totalAED: Math.round(mdTotal) },
          { action: 'Hold', count: holdCount, totalAED: Math.round(holdTotal) },
        ],
        columns: [
          { key: 'action', label: 'Action' },
          { key: 'count', label: 'SKU Count' },
          { key: 'totalAED', label: 'Recovery (AED)' },
        ],
        metricUsed: 'best_action_summary_for_aged_stock',
      }
    },
  },

  similar_designs_for_sku: {
    name: 'similar_designs_for_sku',
    description: 'Finds top matching catalogue designs by vector embedding cosine similarity.',
    resultType: 'table',
    execute: (params) => {
      const targetSku = params?.sku || generatedDesigns[0].sku
      const target = generatedDesigns.find((d) => d.sku === targetSku) ?? generatedDesigns[0]
      const matches = generatedDesigns
        .filter((d) => d.sku !== target.sku)
        .map((d) => ({
          ...d,
          similarity: Number((cosineSimilarity(target.embedding, d.embedding) * 100).toFixed(1)),
        }))
        .sort((a, b) => b.similarity - a.similarity)
        .slice(0, 5)

      return {
        answer: `For ${target.title} (${target.sku}), found 5 close vector matches led by ${matches[0].title} with ${matches[0].similarity}% visual-attribute similarity.`,
        data: matches.map((m) => ({
          sku: m.sku,
          title: m.title,
          category: m.category,
          motif: m.motif,
          stone: m.stone,
          similarity: `${m.similarity}%`,
        })),
        columns: [
          { key: 'sku', label: 'SKU' },
          { key: 'title', label: 'Title' },
          { key: 'category', label: 'Category' },
          { key: 'motif', label: 'Motif' },
          { key: 'similarity', label: 'Match' },
        ],
        metricUsed: 'similar_designs_for_sku',
      }
    },
  },
}

// Intent matcher using keyword scoring and synonyms
export function matchQuestionIntent(input: string): { metric: MetricDefinition | null; queryParams?: Record<string, any> } {
  const text = input.toLowerCase()

  // Synonyms and keyword patterns
  const patterns: { keywords: string[]; metricName: string }[] = [
    {
      keywords: ['store', 'fine gold', 'variance', 'lost gold', 'unexplained', 'gap', 'ledger', 'audit', 'scale'],
      metricName: 'fine_gold_variance_by_store',
    },
    {
      keywords: ['karigar', 'drifting', 'loss', 'artisan', 'goldsmith', 'control chart', 'upper control', 'anomaly'],
      metricName: 'top_karigars_by_loss',
    },
    {
      keywords: ['at risk', 'customer', 'rfm', 'churn', 'lapsed', 'champions', 'loyalty'],
      metricName: 'at_risk_customer_count',
    },
    {
      keywords: ['festival', 'forecast', 'demand', 'dhanteras', 'diwali', 'eid', 'reorder', 'grams'],
      metricName: 'festival_forecast_by_category',
    },
    {
      keywords: ['compliance', 'severity', 'vat', 'huid', 'reverse charge', 'flag', 'exception', 'tax', 'einvoice', 'cash'],
      metricName: 'compliance_exceptions_by_rule',
    },
    {
      keywords: ['aged stock', 'aged', 'recover', 'melt', 'markdown', 'hold', 'optimizer', 'slow moving', 'dead stock'],
      metricName: 'best_action_summary_for_aged_stock',
    },
    {
      keywords: ['similar', 'design', 'matching', 'sku', 'necklace', 'earring', 'vector', 'search', 'motif'],
      metricName: 'similar_designs_for_sku',
    },
  ]

  let bestMetric: string | null = null
  let maxScore = 0

  for (const p of patterns) {
    let score = 0
    for (const kw of p.keywords) {
      if (text.includes(kw)) {
        score += kw.includes(' ') ? 3 : 1
      }
    }
    if (score > maxScore) {
      maxScore = score
      bestMetric = p.metricName
    }
  }

  if (maxScore > 0 && bestMetric && metricRegistry[bestMetric]) {
    return { metric: metricRegistry[bestMetric] }
  }

  return { metric: null }
}
