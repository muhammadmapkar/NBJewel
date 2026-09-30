import { STORES, type Store } from './generateData'

export type Purity = '24K' | '22K' | '18K'

export const PURITY_FACTORS: Record<Purity, number> = {
  '24K': 0.999,
  '22K': 0.916,
  '18K': 0.750,
}

export interface MetalTransaction {
  id: string
  date: string
  karigarId: string
  karigarName: string
  store: Store
  jobId: string
  purity: Purity
  grossIssuedG: number
  fineIssuedG: number
  grossReceivedG: number
  fineReceivedG: number
  lossG: number
  lossPct: number // fine loss percentage = (fineIssued - fineReceived) / fineIssued * 100
  isAnomaly: boolean
}

export interface DailyStoreVariance {
  date: string
  store: Store
  openingFineG: number
  receiptsFineG: number
  salesFineG: number
  karigarIssuesFineG: number
  karigarReturnsFineG: number
  expectedClosingFineG: number
  actualClosingFineG: number
  unexplainedVarianceG: number
  transactions: {
    time: string
    type: 'RECEIPT' | 'SALE' | 'KARIGAR_ISSUE' | 'KARIGAR_RETURN' | 'AUDIT_ADJUSTMENT'
    description: string
    fineG: number
  }[]
}

export interface KarigarSummary {
  id: string
  name: string
  store: Store
  totalJobs: number
  avgLossPct: number
  baselineLossPct: number // first 60 days
  baselineStdDev: number
  uclPct: number
  lclPct: number
  flaggedJobsCount: number
  trend: 'Normal' | 'Drifting Up' | 'Elevated'
  rank: number
}

// Pseudo-random generator with fixed seed
const makeRng = (seed: number) => {
  let state = seed >>> 0
  return () => {
    state = (1664525 * state + 1013904223) >>> 0
    return state / 4294967296
  }
}

export const KARIGARS = [
  { id: 'K01', name: 'Ramesh Verma', store: 'Dubai Deira' as Store }, // drifts up in last 3 weeks
  { id: 'K02', name: 'Suresh Soni', store: 'Sharjah' as Store },     // drifts up in last 3 weeks
  { id: 'K03', name: 'Gopal Swarnakar', store: 'Dubai Deira' as Store },
  { id: 'K04', name: 'Manish Patwa', store: 'Chennai T Nagar' as Store },
  { id: 'K05', name: 'Dinesh Zaveri', store: 'Sharjah' as Store },
  { id: 'K06', name: 'Ashok Karigar', store: 'Dubai Deira' as Store },
  { id: 'K07', name: 'Babulal Sen', store: 'Chennai T Nagar' as Store },
  { id: 'K08', name: 'Kishore Chitrakar', store: 'Dubai Deira' as Store },
  { id: 'K09', name: 'Pradeep Soni', store: 'Sharjah' as Store },
  { id: 'K10', name: 'Santosh Karmakar', store: 'Chennai T Nagar' as Store },
  { id: 'K11', name: 'Vikram Zaveri', store: 'Dubai Deira' as Store },
  { id: 'K12', name: 'Harish Goldsmith', store: 'Sharjah' as Store },
]

export const generatedKarigarData = (() => {
  const rng = makeRng(20260929)
  const days = 90
  const endDate = new Date('2026-09-28T00:00:00Z')
  const purities: Purity[] = ['22K', '22K', '22K', '18K', '24K']

  const transactions: MetalTransaction[] = []
  const dailyLedger: DailyStoreVariance[] = []

  // Store running inventory balances
  const storeBalances: Record<Store, number> = {
    'Dubai Deira': 125000,
    'Sharjah': 88000,
    'Chennai T Nagar': 94000,
  }

  for (let dayIdx = 0; dayIdx < days; dayIdx++) {
    const curDate = new Date(endDate.getTime() - (days - 1 - dayIdx) * 86400000)
    const dateStr = curDate.toISOString().slice(0, 10)
    const isLast3Weeks = dayIdx >= 69 // days 70 to 90

    // Transactions per karigar on this day
    KARIGARS.forEach((k, kIdx) => {
      // 1 to 2 jobs per karigar per day
      const jobCount = rng() > 0.35 ? 1 : 2
      for (let j = 0; j < jobCount; j++) {
        const purity = purities[Math.floor(rng() * purities.length)]
        const factor = PURITY_FACTORS[purity]
        const grossIssued = Number((40 + rng() * 120).toFixed(2))
        const fineIssued = Number((grossIssued * factor).toFixed(3))

        let targetLossPct = 2.85 + (rng() - 0.5) * 0.7 // ~2.5% to 3.2%
        // Two karigars drift up in last 3 weeks to 4.5% - 6%
        if ((k.id === 'K01' || k.id === 'K02') && isLast3Weeks) {
          const driftFactor = ((dayIdx - 69) / 21)
          targetLossPct = 3.2 + driftFactor * (1.8 + rng() * 1.0) // rises up to 4.5 - 6%
        } else if (rng() < 0.02) {
          // rare random spike
          targetLossPct = 4.2 + rng() * 0.9
        }

        const fineReceived = Number((fineIssued * (1 - targetLossPct / 100)).toFixed(3))
        const grossReceived = Number((fineReceived / factor).toFixed(2))
        const fineLoss = Number((fineIssued - fineReceived).toFixed(3))
        const actualLossPct = Number(((fineLoss / fineIssued) * 100).toFixed(2))

        const transId = `JOB-${dateStr.replace(/-/g, '')}-${k.id}-${j + 1}`
        transactions.push({
          id: transId,
          date: dateStr,
          karigarId: k.id,
          karigarName: k.name,
          store: k.store,
          jobId: `JB-${10000 + transactions.length}`,
          purity,
          grossIssuedG: grossIssued,
          fineIssuedG: fineIssued,
          grossReceivedG: grossReceived,
          fineReceivedG: fineReceived,
          lossG: fineLoss,
          lossPct: actualLossPct,
          isAnomaly: false, // will mark after calculating control limits
        })
      }
    })

    // Generate daily variance ledger per store
    STORES.forEach((store) => {
      const opening = storeBalances[store]
      const dayTxs = transactions.filter((t) => t.date === dateStr && t.store === store)
      const dayIssues = dayTxs.reduce((sum, t) => sum + t.fineIssuedG, 0)
      const dayReturns = dayTxs.reduce((sum, t) => sum + t.fineReceivedG, 0)
      const receiptsFine = Number((350 + rng() * 450).toFixed(2))
      const salesFine = Number((320 + rng() * 480).toFixed(2))

      // Sharjah has a small unexplained daily gap of 1.2 to 3.8g fine gold
      let unexplained = 0
      if (store === 'Sharjah') {
        unexplained = Number((1.5 + rng() * 2.1).toFixed(2))
      } else if (rng() < 0.1) {
        // very minor occasional rounding jitter in other stores
        unexplained = Number(((rng() - 0.5) * 0.3).toFixed(2))
      }

      const expectedClosing = Number((opening + receiptsFine + dayReturns - salesFine - dayIssues).toFixed(2))
      const actualClosing = Number((expectedClosing - unexplained).toFixed(2))
      storeBalances[store] = actualClosing

      dailyLedger.push({
        date: dateStr,
        store,
        openingFineG: opening,
        receiptsFineG: receiptsFine,
        salesFineG: salesFine,
        karigarIssuesFineG: Number(dayIssues.toFixed(2)),
        karigarReturnsFineG: Number(dayReturns.toFixed(2)),
        expectedClosingFineG: expectedClosing,
        actualClosingFineG: actualClosing,
        unexplainedVarianceG: unexplained,
        transactions: [
          { time: '09:30', type: 'RECEIPT', description: 'Bullion refinery consignment received', fineG: receiptsFine },
          { time: '11:15', type: 'KARIGAR_ISSUE', description: `${dayTxs.length} workshop jobs issued`, fineG: -dayIssues },
          { time: '16:45', type: 'KARIGAR_RETURN', description: 'Finished jewellery returned from workshop', fineG: dayReturns },
          { time: '20:30', type: 'SALE', description: 'POS aggregate showroom daily gold sales', fineG: -salesFine },
          ...(unexplained !== 0 ? [{
            time: '21:00',
            type: 'AUDIT_ADJUSTMENT' as const,
            description: store === 'Sharjah' ? 'Unexplained scale discrepancy at end of day vault audit' : 'Minor scale calibration variance',
            fineG: -unexplained
          }] : [])
        ]
      })
    })
  }

  // Calculate baseline metrics (first 60 days) per karigar for Control Chart
  const karigarSummaries: KarigarSummary[] = KARIGARS.map((k) => {
    const kJobs = transactions.filter((t) => t.karigarId === k.id)
    const first60Jobs = kJobs.filter((_, idx) => idx < Math.floor(kJobs.length * (60 / 90)))
    const baselineMean = first60Jobs.reduce((sum, j) => sum + j.lossPct, 0) / Math.max(1, first60Jobs.length)
    const variance = first60Jobs.reduce((sum, j) => sum + Math.pow(j.lossPct - baselineMean, 2), 0) / Math.max(1, first60Jobs.length)
    const stdDev = Math.sqrt(variance)
    const ucl = Number((baselineMean + 3 * stdDev).toFixed(2))
    const lcl = Number(Math.max(0, baselineMean - 3 * stdDev).toFixed(2))

    let flaggedCount = 0
    kJobs.forEach((job) => {
      if (job.lossPct > ucl || job.lossPct < lcl) {
        job.isAnomaly = true
        flaggedCount++
      }
    })

    const totalMean = kJobs.reduce((sum, j) => sum + j.lossPct, 0) / Math.max(1, kJobs.length)
    let trend: KarigarSummary['trend'] = 'Normal'
    if (k.id === 'K01' || k.id === 'K02') {
      trend = 'Drifting Up'
    } else if (flaggedCount > 3) {
      trend = 'Elevated'
    }

    return {
      id: k.id,
      name: k.name,
      store: k.store,
      totalJobs: kJobs.length,
      avgLossPct: Number(totalMean.toFixed(2)),
      baselineLossPct: Number(baselineMean.toFixed(2)),
      baselineStdDev: Number(stdDev.toFixed(2)),
      uclPct: ucl,
      lclPct: lcl,
      flaggedJobsCount: flaggedCount,
      trend,
      rank: 1, // calculated next
    }
  })

  // Rank by avgLossPct ascending (lowest loss = rank 1)
  karigarSummaries.sort((a, b) => a.avgLossPct - b.avgLossPct)
  karigarSummaries.forEach((k, idx) => {
    k.rank = idx + 1
  })

  return {
    transactions,
    dailyLedger,
    karigarSummaries,
    currentGoldRateAEDPerG: 312, // approx current gold rate in AED per gram
  }
})()
