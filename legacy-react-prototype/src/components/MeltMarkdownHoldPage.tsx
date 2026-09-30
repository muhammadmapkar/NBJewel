import { useEffect, useState } from 'react'
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
  CartesianGrid,
  Legend,
} from 'recharts'
import {
  Coins,
  DollarSign,
  Download,
  Flame,
  Layers,
  Percent,
  Sliders,
  Sparkles,
  Tag,
  TrendingUp,
} from 'lucide-react'
import {
  getAgedStockOptimization,
  type OptimizerParams,
  type OptimizerResult,
} from '../services/api'
import { CATEGORIES, type Category } from '../data/generateData'
import { SampleBadge } from './Badge'
import { AssumptionsPanel } from './AssumptionsPanel'

const gold = '#d6a84f'
const grid = '#29313e'
const fmt = new Intl.NumberFormat('en-IN', { maximumFractionDigits: 0 })

function Card({ children, className = '' }: { children: React.ReactNode; className?: string }) {
  return <div className={`glass rounded-2xl p-4 sm:p-5 ${className}`}>{children}</div>
}

function ActionKpi({
  action,
  count,
  totalAED,
  icon: Icon,
  colorClass,
}: {
  action: 'Melt' | 'Markdown' | 'Hold'
  count: number
  totalAED: number
  icon: typeof Flame
  colorClass: string
}) {
  return (
    <Card>
      <div className="flex items-start justify-between">
        <div>
          <span className="text-[11px] font-bold uppercase tracking-widest text-slate-500">
            Recommended Action
          </span>
          <div className={`mt-1 text-xl font-extrabold ${colorClass}`}>{action}</div>
        </div>
        <span className="rounded-lg p-2 bg-[#1b222e] text-slate-300">
          <Icon size={18} />
        </span>
      </div>
      <div className="mt-4 flex items-baseline justify-between">
        <div>
          <div className="text-2xl font-extrabold text-slate-100">{fmt.format(count)}</div>
          <div className="text-xs text-slate-500">Aged SKUs</div>
        </div>
        <div className="text-right">
          <div className="text-lg font-bold text-gold">AED {fmt.format(totalAED)}</div>
          <div className="text-[11px] text-slate-500">Net Recovery Value</div>
        </div>
      </div>
    </Card>
  )
}

function Loading() {
  return (
    <div className="space-y-4">
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {[1, 2, 3, 4].map((x) => (
          <div key={x} className="skeleton h-28 rounded-2xl" />
        ))}
      </div>
      <div className="skeleton h-72 rounded-2xl" />
      <div className="skeleton h-80 rounded-2xl" />
    </div>
  )
}

export function MeltMarkdownHoldPage() {
  // Optimizer Control Sliders State
  const [params, setParams] = useState<OptimizerParams>({
    markdownPct: 0.20, // 20%
    refiningLossPct: 0.02, // 2%
    holdingCostPerMonthPct: 0.015, // 1.5%
    goldRateAED: 312,
    categoryFilter: 'All categories',
    actionFilter: 'All actions',
  })

  const [data, setData] = useState<OptimizerResult | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    setLoading(true)
    getAgedStockOptimization(params)
      .then(setData)
      .finally(() => setLoading(false))
  }, [params])

  const exportCsv = () => {
    if (!data) return
    const headers = [
      'SKU',
      'Category',
      'Purity',
      'Gross Weight (g)',
      'Fine Weight (g)',
      'Age (Days)',
      'List Price (AED)',
      'Cost (AED)',
      'Sell Through Score',
      'Melt Value (AED)',
      'Markdown Value (AED)',
      'Hold Value (AED)',
      'Prob Sale 60D',
      'Prob Sale 90D',
      'Recommended Action',
      'Net Value (AED)',
      'Net Gain vs Doing Nothing (AED)',
    ]
    const rows = data.items.map((i) =>
      [
        i.sku,
        `"${i.category}"`,
        i.purity,
        i.grossWeightG,
        i.fineWeightG,
        i.ageDays,
        i.listPrice,
        i.cost,
        i.recentSellThrough,
        i.meltValue,
        i.markdownValue,
        i.holdValue,
        i.probSale60,
        i.probSale90,
        i.bestAction,
        i.bestValue,
        i.netGainAED,
      ].join(',')
    )
    const csv = [headers.join(','), ...rows].join('\n')
    const blob = new Blob([csv], { type: 'text/csv' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `melt-markdown-hold-optimization.csv`
    a.click()
    URL.revokeObjectURL(url)
  }

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <div className="mb-3">
            <SampleBadge />
          </div>
          <h1 className="text-2xl font-extrabold tracking-tight sm:text-3xl">
            Melt, Markdown, or Hold <span className="text-gold">Optimizer</span>
          </h1>
          <p className="mt-1 text-sm text-slate-500">
            Compare salvage outcomes on aged SKUs to unlock locked capital with dynamic sensitivity sliders.
          </p>
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={exportCsv}
            className="flex h-9 items-center gap-2 rounded-lg border border-gold/30 bg-gold/10 px-3 text-xs font-bold text-gold hover:bg-gold/20"
          >
            <Download size={14} /> Export CSV
          </button>
        </div>
      </div>

      {/* Control Sliders Panel */}
      <Card className="border border-[#2f3948] bg-[#10151f]">
        <div className="mb-3 flex items-center justify-between pb-2 border-b border-[#212836]">
          <div className="flex items-center gap-2">
            <Sliders size={16} className="text-gold" />
            <h2 className="text-xs font-bold uppercase tracking-wider text-slate-200">
              Sensitivity & Valuation Assumptions
            </h2>
          </div>
          <span className="text-[11px] text-slate-500">Dynamic model recalculates in real-time</span>
        </div>

        <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
          {/* Gold Rate Slider */}
          <div>
            <div className="flex justify-between text-xs mb-1.5">
              <span className="text-slate-400 font-medium">Spot Gold Rate</span>
              <span className="font-mono font-bold text-gold">AED {params.goldRateAED}/g</span>
            </div>
            <input
              type="range"
              min={260}
              max={380}
              step={1}
              value={params.goldRateAED}
              onChange={(e) => setParams({ ...params, goldRateAED: Number(e.target.value) })}
              className="w-full accent-gold cursor-pointer"
            />
            <div className="flex justify-between text-[10px] text-slate-600 mt-1">
              <span>AED 260</span>
              <span>AED 380</span>
            </div>
          </div>

          {/* Markdown % Slider */}
          <div>
            <div className="flex justify-between text-xs mb-1.5">
              <span className="text-slate-400 font-medium">Proposed Markdown</span>
              <span className="font-mono font-bold text-emerald-400">
                {Math.round(params.markdownPct * 100)}%
              </span>
            </div>
            <input
              type="range"
              min={0.05}
              max={0.45}
              step={0.01}
              value={params.markdownPct}
              onChange={(e) => setParams({ ...params, markdownPct: Number(e.target.value) })}
              className="w-full accent-emerald-400 cursor-pointer"
            />
            <div className="flex justify-between text-[10px] text-slate-600 mt-1">
              <span>5%</span>
              <span>45%</span>
            </div>
          </div>

          {/* Refining Loss % Slider */}
          <div>
            <div className="flex justify-between text-xs mb-1.5">
              <span className="text-slate-400 font-medium">Refining Loss</span>
              <span className="font-mono font-bold text-amber-400">
                {(params.refiningLossPct * 100).toFixed(1)}%
              </span>
            </div>
            <input
              type="range"
              min={0.005}
              max={0.05}
              step={0.005}
              value={params.refiningLossPct}
              onChange={(e) => setParams({ ...params, refiningLossPct: Number(e.target.value) })}
              className="w-full accent-amber-400 cursor-pointer"
            />
            <div className="flex justify-between text-[10px] text-slate-600 mt-1">
              <span>0.5%</span>
              <span>5.0%</span>
            </div>
          </div>

          {/* Holding Cost Slider */}
          <div>
            <div className="flex justify-between text-xs mb-1.5">
              <span className="text-slate-400 font-medium">Monthly Holding Cost</span>
              <span className="font-mono font-bold text-rose-400">
                {(params.holdingCostPerMonthPct * 100).toFixed(1)}% / mo
              </span>
            </div>
            <input
              type="range"
              min={0.005}
              max={0.035}
              step={0.005}
              value={params.holdingCostPerMonthPct}
              onChange={(e) => setParams({ ...params, holdingCostPerMonthPct: Number(e.target.value) })}
              className="w-full accent-rose-400 cursor-pointer"
            />
            <div className="flex justify-between text-[10px] text-slate-600 mt-1">
              <span>0.5%</span>
              <span>3.5%</span>
            </div>
          </div>
        </div>
      </Card>

      {loading || !data ? (
        <Loading />
      ) : (
        <>
          {/* Summary Cards per Action */}
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <ActionKpi
              action="Melt"
              count={data.kpis.meltCount}
              totalAED={data.kpis.meltTotalAED}
              icon={Flame}
              colorClass="text-amber-400"
            />
            <ActionKpi
              action="Markdown"
              count={data.kpis.markdownCount}
              totalAED={data.kpis.markdownTotalAED}
              icon={Tag}
              colorClass="text-emerald-400"
            />
            <ActionKpi
              action="Hold"
              count={data.kpis.holdCount}
              totalAED={data.kpis.holdTotalAED}
              icon={Layers}
              colorClass="text-sky-400"
            />
            <Card>
              <div className="flex items-start justify-between">
                <div>
                  <span className="text-[11px] font-bold uppercase tracking-widest text-slate-500">
                    Net Opportunity
                  </span>
                  <div className="mt-1 text-xl font-extrabold text-gold">Total Impact</div>
                </div>
                <span className="rounded-lg p-2 bg-gold/15 text-gold">
                  <Sparkles size={18} />
                </span>
              </div>
              <div className="mt-4">
                <div className="text-2xl font-extrabold text-slate-100">
                  AED {fmt.format(data.kpis.totalOpportunityAED)}
                </div>
                <div className="text-xs text-slate-500">Incremental capital recovery vs doing nothing</div>
              </div>
            </Card>
          </div>

          {/* Bar Chart: Action Distribution by Category */}
          <Card>
            <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
              <div>
                <h2 className="font-bold text-slate-100">Recommended Action by Jewellery Category</h2>
                <p className="text-xs text-slate-500">
                  Number of SKUs recommended for Melt, Markdown, or Hold across product lines.
                </p>
              </div>
              <div className="flex items-center gap-3 text-[11px] text-slate-400">
                <span className="flex items-center gap-1.5">
                  <span className="h-2.5 w-2.5 rounded bg-amber-500" /> Melt
                </span>
                <span className="flex items-center gap-1.5">
                  <span className="h-2.5 w-2.5 rounded bg-emerald-500" /> Markdown
                </span>
                <span className="flex items-center gap-1.5">
                  <span className="h-2.5 w-2.5 rounded bg-sky-500" /> Hold
                </span>
              </div>
            </div>

            <ResponsiveContainer width="100%" height={260}>
              <BarChart
                data={data.categoryImpact}
                margin={{ top: 10, right: 20, left: 0, bottom: 5 }}
              >
                <CartesianGrid stroke={grid} strokeDasharray="3 3" vertical={false} />
                <XAxis dataKey="category" stroke="#687386" fontSize={11} tickLine={false} />
                <YAxis stroke="#687386" fontSize={10} />
                <Tooltip
                  content={({ active, payload, label }) => {
                    if (active && payload && payload.length) {
                      return (
                        <div className="rounded-lg border border-[#343e4d] bg-[#11161e] p-3 text-xs shadow-xl">
                          <div className="font-bold text-slate-200 mb-1.5">{label}</div>
                          {payload.map((p) => (
                            <div key={p.name} className="flex justify-between gap-4 font-semibold" style={{ color: p.color }}>
                              <span>{p.name}:</span>
                              <span>{p.value} SKUs</span>
                            </div>
                          ))}
                        </div>
                      )
                    }
                    return null
                  }}
                />
                <Bar dataKey="meltCount" name="Melt" fill="#f59e0b" radius={[4, 4, 0, 0]} />
                <Bar dataKey="markdownCount" name="Markdown" fill="#10b981" radius={[4, 4, 0, 0]} />
                <Bar dataKey="holdCount" name="Hold" fill="#0284c7" radius={[4, 4, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </Card>

          {/* Ranked Action Table */}
          <Card>
            <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
              <div>
                <h2 className="font-bold text-slate-100">Ranked SKU Recommendations</h2>
                <p className="text-xs text-slate-500">
                  Sorted by net recovery impact versus doing nothing. Filter by category or recommended action.
                </p>
              </div>

              <div className="flex flex-wrap items-center gap-2">
                <select
                  value={params.categoryFilter}
                  onChange={(e) => setParams({ ...params, categoryFilter: e.target.value as any })}
                  className="h-8 rounded-lg border border-[#303846] bg-[#171d26] px-2.5 text-xs text-slate-200 outline-none focus:border-gold"
                >
                  <option value="All categories">All categories</option>
                  {CATEGORIES.map((c) => (
                    <option key={c} value={c}>{c}</option>
                  ))}
                </select>

                <select
                  value={params.actionFilter}
                  onChange={(e) => setParams({ ...params, actionFilter: e.target.value as any })}
                  className="h-8 rounded-lg border border-[#303846] bg-[#171d26] px-2.5 text-xs text-slate-200 outline-none focus:border-gold"
                >
                  <option value="All actions">All actions</option>
                  <option value="Melt">Melt</option>
                  <option value="Markdown">Markdown</option>
                  <option value="Hold">Hold</option>
                </select>
              </div>
            </div>

            <div className="scrollbar max-h-96 overflow-y-auto overflow-x-auto">
              <table className="w-full min-w-[900px] text-left text-xs">
                <thead className="sticky top-0 z-10 border-b border-[#29313e] bg-[#141a24] text-[10px] uppercase tracking-widest text-slate-400">
                  <tr>
                    <th className="py-2.5 pl-3">SKU</th>
                    <th className="py-2.5">Category</th>
                    <th className="py-2.5">Purity</th>
                    <th className="py-2.5 text-right">Age (Days)</th>
                    <th className="py-2.5 text-right">List (AED)</th>
                    <th className="py-2.5 text-right">Melt Val (AED)</th>
                    <th className="py-2.5 text-right">Markdown (AED)</th>
                    <th className="py-2.5 text-right">Hold Val (AED)</th>
                    <th className="py-2.5 text-center">Action</th>
                    <th className="py-2.5 text-right pr-3">Net Gain (AED)</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#202733]">
                  {data.items.slice(0, 50).map((row) => {
                    const actionTone =
                      row.bestAction === 'Melt'
                        ? 'bg-amber-500/15 text-amber-400 border border-amber-500/30'
                        : row.bestAction === 'Markdown'
                          ? 'bg-emerald-500/15 text-emerald-400 border border-emerald-500/30'
                          : 'bg-sky-500/15 text-sky-400 border border-sky-500/30'

                    return (
                      <tr key={row.sku} className="transition hover:bg-white/[.04]">
                        <td className="py-2.5 pl-3 font-mono font-medium text-slate-300">
                          {row.sku}
                        </td>
                        <td className="py-2.5 text-slate-200">{row.category}</td>
                        <td className="py-2.5 text-slate-400">{row.purity}</td>
                        <td className="py-2.5 text-right font-mono text-slate-400">{row.ageDays}d</td>
                        <td className="py-2.5 text-right font-mono text-slate-300">
                          {fmt.format(row.listPrice)}
                        </td>
                        <td className="py-2.5 text-right font-mono text-slate-400">
                          {fmt.format(row.meltValue)}
                        </td>
                        <td className="py-2.5 text-right font-mono text-slate-400">
                          {fmt.format(row.markdownValue)}
                        </td>
                        <td className="py-2.5 text-right font-mono text-slate-400">
                          {fmt.format(row.holdValue)}
                        </td>
                        <td className="py-2.5 text-center">
                          <span className={`inline-block rounded-full px-2.5 py-0.5 text-[10px] font-bold ${actionTone}`}>
                            {row.bestAction}
                          </span>
                        </td>
                        <td className="py-2.5 pr-3 text-right font-mono font-bold text-gold">
                          +AED {fmt.format(row.netGainAED)}
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          </Card>

          {/* Assumptions Collapsible Panel */}
          <AssumptionsPanel
            title="Model Assumptions and Sensitivity Framework"
            assumptions={[
              'Sale Probabilities Are Modelled, Not Measured: Sale probability in 60 and 90 days is generated from a logistic sigmoid regression based on age in stock, historical sell-through rates, and promotional discount sensitivity.',
              'Melt Value Formula: Fine Weight (g) x Spot Gold Rate (AED) x (1 - Refining Loss %) minus Remake/Refining Cost (estimated at AED 12/g gross).',
              'Markdown Value Formula: List Price x (1 - Markdown %) x Probability of sale in 60 days.',
              'Hold Value Formula: List Price x Probability of sale in 90 days minus cumulative monthly inventory holding cost (3 months).',
              'Doing Nothing Baseline: Estimated at passive scrap liquidation (72% of cost basis). Net gain represents the expected value recovered above this baseline.',
            ]}
            note="Probabilities are modelled based on historical category velocities and retail depreciation curves. Real deployment connects to POS sell-through event streams."
          />
        </>
      )}
    </div>
  )
}
