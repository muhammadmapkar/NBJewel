import { useEffect, useState } from 'react'
import {
  ResponsiveContainer,
  ComposedChart,
  Line,
  XAxis,
  YAxis,
  Tooltip,
  CartesianGrid,
  ReferenceLine,
  Dot,
} from 'recharts'
import {
  AlertTriangle,
  ArrowDownRight,
  ArrowUpRight,
  ChevronRight,
  Coins,
  Download,
  Flame,
  Scale,
  ShieldAlert,
  X,
} from 'lucide-react'
import {
  getGoldReconciliation,
  type GoldReconciliationFilters,
  type GoldReconciliationResponse,
} from '../services/api'
import { STORES, type Store } from '../data/generateData'
import { SampleBadge } from './Badge'
import { AssumptionsPanel } from './AssumptionsPanel'
import type { DailyStoreVariance } from '../data/karigarData'

const gold = '#d6a84f'
const grid = '#29313e'
const fmt = new Intl.NumberFormat('en-IN', { maximumFractionDigits: 1 })
const fmtInt = new Intl.NumberFormat('en-IN', { maximumFractionDigits: 0 })

function Card({ children, className = '' }: { children: React.ReactNode; className?: string }) {
  return <div className={`glass rounded-2xl p-4 sm:p-5 ${className}`}>{children}</div>
}

function Kpi({
  label,
  value,
  note,
  icon: Icon,
  tone = 'gold',
}: {
  label: string
  value: string
  note: string
  icon: typeof Coins
  tone?: 'gold' | 'red' | 'mint'
}) {
  return (
    <Card>
      <div className="flex items-start justify-between">
        <span className="text-[11px] font-bold uppercase tracking-widest text-slate-500">{label}</span>
        <span
          className={`rounded-lg p-2 ${
            tone === 'red'
              ? 'bg-danger/10 text-danger'
              : tone === 'mint'
                ? 'bg-mint/10 text-mint'
                : 'bg-gold/10 text-gold'
          }`}
        >
          <Icon size={16} />
        </span>
      </div>
      <div className="mt-4 text-2xl font-extrabold tracking-tight text-slate-100">{value}</div>
      <div className="mt-1 text-xs text-slate-500">{note}</div>
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
      <div className="skeleton h-64 rounded-2xl" />
    </div>
  )
}

export function GoldReconciliationPage() {
  const [filters, setFilters] = useState<GoldReconciliationFilters>({
    store: 'All stores',
    karigarId: 'All karigars',
    from: '2026-06-30',
    to: '2026-09-28',
  })
  const [data, setData] = useState<GoldReconciliationResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [selectedLedgerRow, setSelectedLedgerRow] = useState<DailyStoreVariance | null>(null)

  useEffect(() => {
    setLoading(true)
    getGoldReconciliation(filters)
      .then(setData)
      .finally(() => setLoading(false))
  }, [filters])

  const exportCsv = () => {
    if (!data) return
    const headers = [
      'Date',
      'Store',
      'Opening Fine (g)',
      'Receipts (g)',
      'Karigar Returns (g)',
      'Sales (g)',
      'Karigar Issues (g)',
      'Expected Closing (g)',
      'Actual Closing (g)',
      'Unexplained Variance (g)',
    ]
    const rows = data.ledger.map((r) =>
      [
        r.date,
        `"${r.store}"`,
        r.openingFineG,
        r.receiptsFineG,
        r.karigarReturnsFineG,
        r.salesFineG,
        r.karigarIssuesFineG,
        r.expectedClosingFineG,
        r.actualClosingFineG,
        r.unexplainedVarianceG,
      ].join(',')
    )
    const csv = [headers.join(','), ...rows].join('\n')
    const blob = new Blob([csv], { type: 'text/csv' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `fine-gold-reconciliation-${filters.store.replace(/\s+/g, '-')}.csv`
    a.click()
    URL.revokeObjectURL(url)
  }

  // Custom dot for Control Chart to mark points outside control limits in red
  const renderControlPoint = (props: any) => {
    const { cx, cy, payload } = props
    if (payload.isAnomaly) {
      return (
        <circle
          key={`dot-${payload.jobId}`}
          cx={cx}
          cy={cy}
          r={5}
          fill="#ef4444"
          stroke="#fee2e2"
          strokeWidth={1.5}
        />
      )
    }
    return (
      <circle
        key={`dot-${payload.jobId}`}
        cx={cx}
        cy={cy}
        r={2.5}
        fill={gold}
      />
    )
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
            Fine-Gold <span className="text-gold">Reconciliation</span>
          </h1>
          <p className="mt-1 text-sm text-slate-500">
            Vault balance audits, unexplained variance ledger, and workshop statistical process control.
          </p>
        </div>

        {/* Global Controls & Filters */}
        <div className="flex flex-wrap items-end gap-2.5">
          <label className="flex min-w-[130px] flex-1 flex-col gap-1 text-[10px] font-bold uppercase tracking-widest text-slate-500">
            Store
            <select
              value={filters.store}
              onChange={(e) => setFilters({ ...filters, store: e.target.value as Store | 'All stores' })}
              className="h-9 rounded-lg border border-[#303846] bg-[#171d26] px-3 text-xs text-slate-200 outline-none focus:border-gold"
            >
              {['All stores', ...STORES].map((s) => (
                <option key={s}>{s}</option>
              ))}
            </select>
          </label>

          <label className="flex min-w-[150px] flex-1 flex-col gap-1 text-[10px] font-bold uppercase tracking-widest text-slate-500">
            Karigar (Control Chart)
            <select
              value={filters.karigarId}
              onChange={(e) => setFilters({ ...filters, karigarId: e.target.value })}
              className="h-9 rounded-lg border border-[#303846] bg-[#171d26] px-3 text-xs text-slate-200 outline-none focus:border-gold"
            >
              <option value="All karigars">All karigars (Default: Ramesh Verma)</option>
              {data?.allKarigars.map((k) => (
                <option key={k.id} value={k.id}>
                  {k.name} ({k.store})
                </option>
              ))}
            </select>
          </label>

          <label className="flex min-w-[125px] flex-1 flex-col gap-1 text-[10px] font-bold uppercase tracking-widest text-slate-500">
            From
            <input
              type="date"
              value={filters.from}
              onChange={(e) => setFilters({ ...filters, from: e.target.value })}
              className="h-9 rounded-lg border border-[#303846] bg-[#171d26] px-3 text-xs text-slate-200 outline-none focus:border-gold"
            >
            </input>
          </label>

          <label className="flex min-w-[125px] flex-1 flex-col gap-1 text-[10px] font-bold uppercase tracking-widest text-slate-500">
            To
            <input
              type="date"
              value={filters.to}
              onChange={(e) => setFilters({ ...filters, to: e.target.value })}
              className="h-9 rounded-lg border border-[#303846] bg-[#171d26] px-3 text-xs text-slate-200 outline-none focus:border-gold"
            >
            </input>
          </label>

          <button
            onClick={exportCsv}
            className="flex h-9 items-center gap-2 rounded-lg border border-gold/30 bg-gold/10 px-3 text-xs font-bold text-gold hover:bg-gold/20"
          >
            <Download size={14} /> Export CSV
          </button>
        </div>
      </div>

      {loading || !data ? (
        <Loading />
      ) : (
        <>
          {/* KPI Cards */}
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <Kpi
              label="Fine gold in stock"
              value={`${fmtInt.format(data.kpis.fineGoldInStockG)}g`}
              note={filters.store === 'All stores' ? 'Consolidated vault physical balance' : `${filters.store} vault`}
              icon={Coins}
              tone="mint"
            />
            <Kpi
              label="Unexplained variance"
              value={`${fmt.format(data.kpis.unexplainedVarianceG)}g`}
              note="Cumulative unexplained balance gap"
              icon={Scale}
              tone={data.kpis.unexplainedVarianceG > 50 ? 'red' : 'gold'}
            />
            <Kpi
              label="Variance value"
              value={`AED ${fmtInt.format(data.kpis.varianceValueAED)}`}
              note={`Valued at AED ${data.kpis.currentGoldRateAED}/g spot`}
              icon={ShieldAlert}
              tone="red"
            />
            <Kpi
              label="Worst store"
              value={data.kpis.worstStore}
              note="Highest unexplained variance over window"
              icon={AlertTriangle}
              tone="red"
            />
          </div>

          {/* Daily Variance Ledger Table */}
          <Card>
            <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
              <div>
                <h2 className="font-bold text-slate-100">Daily Fine-Gold Variance Ledger</h2>
                <p className="text-xs text-slate-500">
                  Formula: Opening + Receipts + Workshop Returns - Sales - Workshop Issues vs Closing. Click any row to inspect daily transactions.
                </p>
              </div>
              <span className="font-mono text-[10px] text-slate-500">90-DAY AUDIT TRAIL</span>
            </div>

            <div className="scrollbar max-h-80 overflow-y-auto overflow-x-auto">
              <table className="w-full min-w-[840px] text-left text-xs">
                <thead className="sticky top-0 z-10 border-b border-[#29313e] bg-[#141a24] text-[10px] uppercase tracking-widest text-slate-400">
                  <tr>
                    <th className="py-2.5 pl-3">Date</th>
                    <th className="py-2.5">Store</th>
                    <th className="py-2.5 text-right">Opening (g)</th>
                    <th className="py-2.5 text-right">Receipts (g)</th>
                    <th className="py-2.5 text-right">Sales (g)</th>
                    <th className="py-2.5 text-right">Issues (g)</th>
                    <th className="py-2.5 text-right">Expected (g)</th>
                    <th className="py-2.5 text-right">Actual (g)</th>
                    <th className="py-2.5 text-right pr-3">Unexplained (g)</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#202733]">
                  {data.ledger.slice(0, 30).map((row, idx) => {
                    const hasDiscrepancy = row.unexplainedVarianceG > 0.5
                    const isSelected = selectedLedgerRow?.date === row.date && selectedLedgerRow?.store === row.store
                    return (
                      <tr
                        key={`${row.date}-${row.store}-${idx}`}
                        onClick={() => setSelectedLedgerRow(row)}
                        className={`cursor-pointer transition hover:bg-white/[.04] ${
                          isSelected ? 'bg-gold/[.08]' : hasDiscrepancy ? 'bg-danger/[.03]' : ''
                        }`}
                      >
                        <td className="py-2.5 pl-3 font-mono font-medium text-slate-300">{row.date}</td>
                        <td className="py-2.5 text-slate-200">{row.store}</td>
                        <td className="py-2.5 text-right text-slate-400">{fmt.format(row.openingFineG)}</td>
                        <td className="py-2.5 text-right text-emerald-400">+{fmt.format(row.receiptsFineG)}</td>
                        <td className="py-2.5 text-right text-amber-400">-{fmt.format(row.salesFineG)}</td>
                        <td className="py-2.5 text-right text-slate-400">-{fmt.format(row.karigarIssuesFineG)}</td>
                        <td className="py-2.5 text-right text-slate-300">{fmt.format(row.expectedClosingFineG)}</td>
                        <td className="py-2.5 text-right font-medium text-slate-200">{fmt.format(row.actualClosingFineG)}</td>
                        <td className="py-2.5 pr-3 text-right">
                          {hasDiscrepancy ? (
                            <span className="inline-flex items-center gap-1 rounded bg-danger/15 px-1.5 py-0.5 font-mono text-[11px] font-bold text-danger">
                              <ArrowDownRight size={12} /> {fmt.format(row.unexplainedVarianceG)}g
                            </span>
                          ) : (
                            <span className="font-mono text-slate-500">0.0g</span>
                          )}
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>

            {/* Drilldown modal / drawer when row clicked */}
            {selectedLedgerRow && (
              <div className="mt-4 rounded-xl border border-gold/30 bg-[#10151f] p-4 text-xs">
                <div className="flex items-center justify-between pb-3 border-b border-[#252e3d]">
                  <div className="flex items-center gap-2">
                    <span className="rounded bg-gold/15 px-2 py-0.5 font-bold uppercase text-gold text-[10px]">
                      Day Drilldown
                    </span>
                    <span className="font-bold text-slate-200">
                      {selectedLedgerRow.store} - {selectedLedgerRow.date}
                    </span>
                  </div>
                  <button
                    onClick={() => setSelectedLedgerRow(null)}
                    className="rounded p-1 text-slate-400 hover:text-slate-100 hover:bg-white/10"
                  >
                    <X size={15} />
                  </button>
                </div>
                <div className="mt-3 grid gap-2 sm:grid-cols-2 lg:grid-cols-4 text-slate-400">
                  <div>Opening: <span className="font-mono text-slate-200">{fmt.format(selectedLedgerRow.openingFineG)}g</span></div>
                  <div>Expected Closing: <span className="font-mono text-slate-200">{fmt.format(selectedLedgerRow.expectedClosingFineG)}g</span></div>
                  <div>Actual Closing: <span className="font-mono text-slate-200">{fmt.format(selectedLedgerRow.actualClosingFineG)}g</span></div>
                  <div>
                    Unexplained: <span className="font-mono font-bold text-danger">{fmt.format(selectedLedgerRow.unexplainedVarianceG)}g</span>
                  </div>
                </div>
                <div className="mt-3">
                  <div className="text-[10px] font-bold uppercase tracking-wider text-slate-500 mb-1.5">
                    Day Activity Log
                  </div>
                  <div className="space-y-1">
                    {selectedLedgerRow.transactions.map((tx, i) => (
                      <div
                        key={i}
                        className={`flex items-center justify-between rounded px-2.5 py-1.5 ${
                          tx.type === 'AUDIT_ADJUSTMENT' ? 'bg-danger/10 border border-danger/20 text-danger' : 'bg-[#181f2c]'
                        }`}
                      >
                        <div className="flex items-center gap-2">
                          <span className="font-mono text-[10px] text-slate-400">{tx.time}</span>
                          <span className="text-slate-200 font-medium">{tx.description}</span>
                        </div>
                        <span className="font-mono font-bold">
                          {tx.fineG > 0 ? `+${fmt.format(tx.fineG)}g` : `${fmt.format(tx.fineG)}g`}
                        </span>
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            )}
          </Card>

          {/* Karigar Control Chart */}
          <Card>
            <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
              <div>
                <div className="flex items-center gap-2">
                  <h2 className="font-bold text-slate-100">
                    Karigar Statistical Control Chart: {data.controlChart.karigarName}
                  </h2>
                  <span className="rounded bg-gold/15 px-2 py-0.5 text-[10px] font-bold text-gold">
                    SPC 3-Sigma Limits
                  </span>
                </div>
                <p className="mt-1 text-xs text-slate-500">
                  Job metal loss % plotted over time. Baseline established from first 60 days. Red dots mark points breaching Upper Control Limit (UCL = {data.controlChart.ucl}%).
                </p>
              </div>

              <div className="flex flex-wrap items-center gap-3 text-[11px] text-slate-400">
                <span className="flex items-center gap-1.5">
                  <span className="h-2 w-2 rounded-full bg-gold" /> Job Loss %
                </span>
                <span className="flex items-center gap-1.5">
                  <span className="h-0.5 w-4 bg-emerald-500" /> Mean ({data.controlChart.baselineMean}%)
                </span>
                <span className="flex items-center gap-1.5">
                  <span className="h-0.5 w-4 bg-danger" /> UCL ({data.controlChart.ucl}%)
                </span>
                <span className="flex items-center gap-1.5">
                  <span className="h-2.5 w-2.5 rounded-full bg-danger border border-white" /> Anomaly
                </span>
              </div>
            </div>

            <ResponsiveContainer width="100%" height={290}>
              <ComposedChart
                data={data.controlChart.jobs}
                margin={{ top: 10, right: 20, left: 0, bottom: 5 }}
              >
                <CartesianGrid stroke={grid} strokeDasharray="3 3" vertical={false} />
                <XAxis
                  dataKey="date"
                  stroke="#687386"
                  fontSize={10}
                  tickFormatter={(v) => v.slice(5)}
                  interval={Math.floor(data.controlChart.jobs.length / 10)}
                />
                <YAxis
                  stroke="#687386"
                  fontSize={10}
                  domain={[1.5, 7.0]}
                  tickFormatter={(v) => `${v}%`}
                />
                <Tooltip
                  content={({ active, payload }) => {
                    if (active && payload && payload.length) {
                      const p = payload[0].payload
                      return (
                        <div className="rounded-lg border border-[#343e4d] bg-[#11161e] p-3 text-xs shadow-xl">
                          <div className="font-bold text-slate-200">Job: {p.jobId}</div>
                          <div className="text-[11px] text-slate-400 mb-1">{p.date}</div>
                          <div className="font-semibold" style={{ color: p.isAnomaly ? '#ef4444' : gold }}>
                            Loss: {p.lossPct}% {p.isAnomaly ? '(Breached UCL)' : ''}
                          </div>
                          <div className="text-[11px] text-slate-400 mt-1">
                            Issued: {p.fineIssuedG}g | Received: {p.fineReceivedG}g
                          </div>
                        </div>
                      )
                    }
                    return null
                  }}
                />
                <ReferenceLine
                  y={data.controlChart.ucl}
                  stroke="#ef4444"
                  strokeDasharray="4 4"
                  label={{ value: `UCL ${data.controlChart.ucl}%`, fill: '#ef4444', fontSize: 10, position: 'right' }}
                />
                <ReferenceLine
                  y={data.controlChart.baselineMean}
                  stroke="#10b981"
                  strokeDasharray="4 4"
                  label={{ value: `Mean ${data.controlChart.baselineMean}%`, fill: '#10b981', fontSize: 10, position: 'right' }}
                />
                <ReferenceLine
                  y={data.controlChart.lcl}
                  stroke="#64748b"
                  strokeDasharray="4 4"
                  label={{ value: `LCL ${data.controlChart.lcl}%`, fill: '#64748b', fontSize: 10, position: 'right' }}
                />
                <Line
                  type="monotone"
                  dataKey="lossPct"
                  stroke={gold}
                  strokeWidth={1.5}
                  dot={renderControlPoint}
                  isAnimationActive={false}
                />
              </ComposedChart>
            </ResponsiveContainer>
          </Card>

          {/* Karigar Scorecard Table */}
          <Card>
            <div className="mb-4 flex items-center justify-between">
              <div>
                <h2 className="font-bold text-slate-100">Karigar Workshop Scorecard</h2>
                <p className="text-xs text-slate-500">
                  Ranking across artisans by average loss percentage, trend drift, and flagged anomaly jobs.
                </p>
              </div>
              <span className="font-mono text-[10px] text-slate-500">12 ARTISANS</span>
            </div>

            <div className="scrollbar overflow-x-auto">
              <table className="w-full min-w-[700px] text-left text-xs">
                <thead className="border-b border-[#29313e] text-[10px] uppercase tracking-widest text-slate-400">
                  <tr>
                    <th className="pb-3 pl-2">Rank</th>
                    <th className="pb-3">Karigar</th>
                    <th className="pb-3">Store</th>
                    <th className="pb-3 text-right">Total Jobs</th>
                    <th className="pb-3 text-right">Avg Loss %</th>
                    <th className="pb-3 text-right">Baseline %</th>
                    <th className="pb-3 text-right">UCL %</th>
                    <th className="pb-3 text-center">Trend</th>
                    <th className="pb-3 text-right pr-2">Flagged Jobs</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#202733]">
                  {data.scorecard.map((k) => {
                    const isDrifting = k.trend === 'Drifting Up'
                    return (
                      <tr
                        key={k.id}
                        onClick={() => setFilters({ ...filters, karigarId: k.id })}
                        className={`cursor-pointer transition hover:bg-white/[.04] ${
                          filters.karigarId === k.id ? 'bg-gold/[.08]' : isDrifting ? 'bg-danger/[.04]' : ''
                        }`}
                      >
                        <td className="py-3 pl-2 font-mono font-bold text-slate-400">#{k.rank}</td>
                        <td className="py-3 font-semibold text-slate-200 flex items-center gap-2">
                          {k.name}
                          {isDrifting && (
                            <span className="inline-flex items-center gap-0.5 rounded bg-danger/15 px-1.5 py-0.5 text-[9px] font-bold text-danger">
                              <Flame size={10} /> DRIFT
                            </span>
                          )}
                        </td>
                        <td className="py-3 text-slate-400">{k.store}</td>
                        <td className="py-3 text-right font-mono text-slate-300">{k.totalJobs}</td>
                        <td className="py-3 text-right font-mono font-bold text-slate-100">{k.avgLossPct}%</td>
                        <td className="py-3 text-right font-mono text-slate-400">{k.baselineLossPct}%</td>
                        <td className="py-3 text-right font-mono text-slate-400">{k.uclPct}%</td>
                        <td className="py-3 text-center">
                          <span
                            className={`rounded-full px-2 py-0.5 text-[10px] font-bold ${
                              k.trend === 'Drifting Up'
                                ? 'bg-danger/15 text-danger border border-danger/30'
                                : k.trend === 'Elevated'
                                  ? 'bg-gold/15 text-gold border border-gold/30'
                                  : 'bg-mint/15 text-mint border border-mint/30'
                            }`}
                          >
                            {k.trend}
                          </span>
                        </td>
                        <td className="py-3 pr-2 text-right">
                          <span
                            className={`font-mono font-bold ${
                              k.flaggedJobsCount > 0 ? 'text-danger' : 'text-slate-500'
                            }`}
                          >
                            {k.flaggedJobsCount}
                          </span>
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
            title="Fine-Gold Reconciliation Assumptions"
            assumptions={[
              'Norm Loss Benchmark: A standard tolerance of 3.0% fine-gold loss is expected for normal filing, polishing, and soldering operations.',
              'Fine Weight Conversion: Fine Weight = Gross Weight multiplied by purity factor (24K: 0.999, 22K: 0.916, 18K: 0.750).',
              'Statistical Process Control (SPC): Control limits are calculated using the first 60 days baseline per karigar (Mean +/- 3 standard deviations). Any job outside UCL/LCL is flagged as an anomaly.',
              'Unexplained Variance: Calculated as closing actual vault weight minus expected mathematical balance. Sharjah includes a simulated daily scale discrepancy for demonstration.',
            ]}
            note="In production, daily scale calibrations, melting crucible dust recovery, and floor sweeps are credited weekly against karigar ledger balances."
          />
        </>
      )}
    </div>
  )
}
