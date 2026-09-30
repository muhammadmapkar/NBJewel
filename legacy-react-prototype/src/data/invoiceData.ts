import { STORES, type Store } from './generateData'

export interface Invoice {
  id: string
  date: string
  store: Store
  country: 'UAE' | 'India'
  buyerType: 'B2C' | 'B2B'
  buyerVatRegistered: boolean
  buyerDeclarationOnFile: boolean
  reverseChargeApplied: boolean
  vatOnMetal: number // AED or INR
  vatOnMaking: number // AED or INR
  metalValue: number
  makingCharges: number
  cashAmount: number
  totalAmount: number
  huid?: string
  huidInRegister?: boolean
  isOldGoldTradeIn?: boolean
  marginLedgerTagged?: boolean
  hasEInvoiceFields?: boolean
  isSilver?: boolean
  silverHallmarked?: boolean
  injectedErrorType?: string
}

const makeRng = (seed: number) => {
  let state = seed >>> 0
  return () => {
    state = (1664525 * state + 1013904223) >>> 0
    return state / 4294967296
  }
}

export const generatedInvoices: Invoice[] = (() => {
  const rng = makeRng(20260930)
  const list: Invoice[] = []
  const count = 1500
  const endDate = new Date('2026-09-28T00:00:00Z')

  // Error rules to sample for ~6% overall error injection
  const uaeErrorPool = ['U1', 'U2', 'U3', 'U4', 'U5']
  const indiaErrorPool = ['I1', 'I2', 'I3', 'I4', 'I5', 'I6']

  for (let i = 0; i < count; i++) {
    const daysAgo = Math.floor(rng() * 90)
    const dateObj = new Date(endDate.getTime() - daysAgo * 86400000)
    const date = dateObj.toISOString().slice(0, 10)

    // ~65% UAE stores ('Dubai Deira', 'Sharjah'), ~35% India ('Chennai T Nagar')
    const isIndia = rng() < 0.35
    const store: Store = isIndia
      ? 'Chennai T Nagar'
      : (rng() < 0.55 ? 'Dubai Deira' : 'Sharjah')
    const country = isIndia ? 'India' : 'UAE'

    const isB2B = rng() < 0.18
    let buyerType: 'B2C' | 'B2B' = isB2B ? 'B2B' : 'B2C'

    // Amounts
    const metalVal = isIndia
      ? Math.round(45000 + rng() * 320000)
      : Math.round(2500 + rng() * 28000)
    const making = Math.round(metalVal * (0.08 + rng() * 0.12))
    const subtotal = metalVal + making

    // Default clean state
    let buyerVatRegistered = isB2B ? rng() > 0.1 : false
    let reverseChargeApplied = country === 'UAE' && isB2B && buyerVatRegistered && rng() > 0.4
    let buyerDeclarationOnFile = reverseChargeApplied ? true : rng() > 0.5
    let vatOnMetal = country === 'UAE' ? (reverseChargeApplied ? 0 : Math.round(metalVal * 0.05)) : 0
    let vatOnMaking = country === 'UAE' ? (reverseChargeApplied ? 0 : Math.round(making * 0.05)) : 0
    let cashAmount = rng() < 0.3 ? Math.round(subtotal * (0.1 + rng() * 0.5)) : 0
    const totalAmount = subtotal + vatOnMetal + vatOnMaking

    let huid: string | undefined = undefined
    let huidInRegister: boolean | undefined = undefined
    let isOldGoldTradeIn = rng() < 0.15
    let marginLedgerTagged = isOldGoldTradeIn ? true : undefined
    let hasEInvoiceFields = isB2B ? true : (rng() > 0.3)
    let isSilver = rng() < 0.1
    let silverHallmarked = isSilver ? true : undefined

    if (country === 'India' && !isSilver) {
      // 6-digit alphanumeric HUID
      const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'
      let code = ''
      for (let c = 0; c < 6; c++) code += chars[Math.floor(rng() * chars.length)]
      huid = code
      huidInRegister = true
    }

    // ~6% overall error injection
    let injectedErrorType: string | undefined = undefined
    if (rng() < 0.065) {
      if (country === 'UAE') {
        const err = uaeErrorPool[Math.floor(rng() * uaeErrorPool.length)]
        injectedErrorType = err
        switch (err) {
          case 'U1':
            // Retail B2C missing 5% VAT on metal or making
            if (buyerType === 'B2C') {
              if (rng() < 0.5) vatOnMetal = 0
              else vatOnMaking = 0
            } else {
              // B2B without reverse charge missing VAT
              reverseChargeApplied = false
              vatOnMetal = 0
            }
            break
          case 'U2':
            // B2B reverse charge without declaration on file
            buyerType = 'B2B'
            buyerVatRegistered = true
            reverseChargeApplied = true
            buyerDeclarationOnFile = false
            vatOnMetal = 0
            vatOnMaking = 0
            break
          case 'U3':
            // Reverse charge applied to non-registered buyer
            buyerType = 'B2B'
            buyerVatRegistered = false
            reverseChargeApplied = true
            buyerDeclarationOnFile = true
            vatOnMetal = 0
            vatOnMaking = 0
            break
          case 'U4':
            // VAT charged on an invoice where reverse charge is marked
            buyerType = 'B2B'
            buyerVatRegistered = true
            reverseChargeApplied = true
            vatOnMetal = Math.round(metalVal * 0.05)
            vatOnMaking = Math.round(making * 0.05)
            break
          case 'U5':
            // Missing e-invoicing fields
            hasEInvoiceFields = false
            break
        }
      } else {
        // India
        const err = indiaErrorPool[Math.floor(rng() * indiaErrorPool.length)]
        injectedErrorType = err
        switch (err) {
          case 'I1':
            // Gold sold with no HUID
            if (!isSilver) huid = undefined
            break
          case 'I2':
            // HUID not found in hallmark register
            if (!isSilver && huid) huidInRegister = false
            break
          case 'I3':
            // Cash sale > 200,000 INR flagged for TCS review
            cashAmount = Math.round(210000 + rng() * 95000)
            break
          case 'I4':
            // Cash receipt > 200,000 INR Section 269ST cap violation
            cashAmount = Math.round(205000 + rng() * 120000)
            break
          case 'I5':
            // Old gold trade-in without margin-scheme ledger tag
            isOldGoldTradeIn = true
            marginLedgerTagged = false
            break
          case 'I6':
            // Silver hallmark check (off by default)
            isSilver = true
            silverHallmarked = false
            break
        }
      }
    }

    const prefix = country === 'UAE' ? 'INV-DXB' : 'INV-CHN'
    list.push({
      id: `${prefix}-${100000 + i}`,
      date,
      store,
      country,
      buyerType,
      buyerVatRegistered,
      buyerDeclarationOnFile,
      reverseChargeApplied,
      vatOnMetal,
      vatOnMaking,
      metalValue: metalVal,
      makingCharges: making,
      cashAmount,
      totalAmount,
      huid,
      huidInRegister,
      isOldGoldTradeIn,
      marginLedgerTagged,
      hasEInvoiceFields,
      isSilver,
      silverHallmarked,
      injectedErrorType,
    })
  }

  return list
})()
