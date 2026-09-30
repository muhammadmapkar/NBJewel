import type { Invoice } from '../data/invoiceData'
import type { ComplianceConfig } from '../config/complianceConfig'

export type Severity = 'High' | 'Medium' | 'Low'

export interface ComplianceException {
  invoiceId: string
  date: string
  store: string
  country: 'UAE' | 'India'
  ruleId: string
  ruleName: string
  severity: Severity
  reason: string
  suggestedFix: string
  valueAtRisk: number // Currency amount at risk
  currency: 'AED' | 'INR'
}

export interface ComplianceRule {
  id: string
  name: string
  severity: Severity
  country: 'UAE' | 'India'
  description: string
  suggestedFix: string
  evaluate: (invoice: Invoice, config: ComplianceConfig) => ComplianceException | null
}

export const uaeRules: ComplianceRule[] = [
  {
    id: 'U1',
    name: 'Retail Missing 5% VAT',
    severity: 'High',
    country: 'UAE',
    description: 'Retail jewellery invoice missing standard 5% VAT on metal or on making charges.',
    suggestedFix: 'Re-issue invoice with 5% VAT computed on both fine metal value and making charges, or attach tax-exempt statutory justification.',
    evaluate: (inv, config) => {
      if (inv.country !== 'UAE') return null
      // If retail B2C or B2B without reverse charge
      if (!inv.reverseChargeApplied) {
        const expectedMetalVat = Math.round(inv.metalValue * config.uaeVatRate)
        const expectedMakingVat = Math.round(inv.makingCharges * config.uaeVatRate)
        const missingMetal = inv.vatOnMetal < expectedMetalVat * 0.95
        const missingMaking = inv.vatOnMaking < expectedMakingVat * 0.95
        if (missingMetal || missingMaking) {
          const delta = (expectedMetalVat - inv.vatOnMetal) + (expectedMakingVat - inv.vatOnMaking)
          return {
            invoiceId: inv.id,
            date: inv.date,
            store: inv.store,
            country: 'UAE',
            ruleId: 'U1',
            ruleName: 'Retail Missing 5% VAT',
            severity: 'High',
            reason: missingMetal && missingMaking
              ? 'Zero or deficient VAT applied to both metal value and making charges'
              : missingMetal
                ? 'Missing 5% VAT on metal value'
                : 'Missing 5% VAT on making charges',
            suggestedFix: 'Re-issue invoice with 5% VAT computed on both fine metal value and making charges, or attach tax-exempt statutory justification.',
            valueAtRisk: Math.max(10, delta),
            currency: 'AED',
          }
        }
      }
      return null
    },
  },
  {
    id: 'U2',
    name: 'B2B Reverse Charge Without Declaration',
    severity: 'High',
    country: 'UAE',
    description: 'B2B reverse charge invoice with no buyer declaration on file.',
    suggestedFix: 'Obtain signed FTA Form VAT 201 recipient declaration and archive prior to quarterly tax filing.',
    evaluate: (inv, config) => {
      if (inv.country !== 'UAE') return null
      if (config.uaeRequireB2BDeclaration && inv.buyerType === 'B2B' && inv.reverseChargeApplied && !inv.buyerDeclarationOnFile) {
        return {
          invoiceId: inv.id,
          date: inv.date,
          store: inv.store,
          country: 'UAE',
          ruleId: 'U2',
          ruleName: 'B2B Reverse Charge Without Declaration',
          severity: 'High',
          reason: 'Reverse charge mechanism applied without verified recipient written declaration on record',
          suggestedFix: 'Obtain signed FTA Form VAT 201 recipient declaration and archive prior to quarterly tax filing.',
          valueAtRisk: Math.round(inv.totalAmount * config.uaeVatRate),
          currency: 'AED',
        }
      }
      return null
    },
  },
  {
    id: 'U3',
    name: 'Reverse Charge to Unregistered Buyer',
    severity: 'High',
    country: 'UAE',
    description: 'Reverse charge applied to a buyer who is not VAT registered.',
    suggestedFix: 'Cancel reverse charge tagging, verify TRN on FTA portal, and collect 5% standard VAT.',
    evaluate: (inv, config) => {
      if (inv.country !== 'UAE') return null
      if (inv.reverseChargeApplied && !inv.buyerVatRegistered) {
        return {
          invoiceId: inv.id,
          date: inv.date,
          store: inv.store,
          country: 'UAE',
          ruleId: 'U3',
          ruleName: 'Reverse Charge to Unregistered Buyer',
          severity: 'High',
          reason: 'Reverse charge was tagged for a buyer who does not hold a valid VAT TRN',
          suggestedFix: 'Cancel reverse charge tagging, verify TRN on FTA portal, and collect 5% standard VAT.',
          valueAtRisk: Math.round(inv.totalAmount * config.uaeVatRate),
          currency: 'AED',
        }
      }
      return null
    },
  },
  {
    id: 'U4',
    name: 'VAT Charged Under Reverse Charge',
    severity: 'Medium',
    country: 'UAE',
    description: 'VAT charged on an invoice where reverse charge is marked.',
    suggestedFix: 'Issue credit note for improperly collected VAT amount and re-issue net reverse charge invoice.',
    evaluate: (inv) => {
      if (inv.country !== 'UAE') return null
      if (inv.reverseChargeApplied && (inv.vatOnMetal > 0 || inv.vatOnMaking > 0)) {
        return {
          invoiceId: inv.id,
          date: inv.date,
          store: inv.store,
          country: 'UAE',
          ruleId: 'U4',
          ruleName: 'VAT Charged Under Reverse Charge',
          severity: 'Medium',
          reason: 'Both reverse charge flag and positive output VAT are present, creating accounting duplication',
          suggestedFix: 'Issue credit note for improperly collected VAT amount and re-issue net reverse charge invoice.',
          valueAtRisk: inv.vatOnMetal + inv.vatOnMaking,
          currency: 'AED',
        }
      }
      return null
    },
  },
  {
    id: 'U5',
    name: 'Missing E-Invoicing Metadata',
    severity: 'Low',
    country: 'UAE',
    description: 'Invoice missing fields required for e-invoicing (placeholder rule, configurable).',
    suggestedFix: 'Enrich invoice payload with mandatory UUID, buyer QR code hash, and cryptographic stamp before ERP submission.',
    evaluate: (inv, config) => {
      if (inv.country !== 'UAE') return null
      if (config.uaeEInvoicingEnforced && inv.hasEInvoiceFields === false) {
        return {
          invoiceId: inv.id,
          date: inv.date,
          store: inv.store,
          country: 'UAE',
          ruleId: 'U5',
          ruleName: 'Missing E-Invoicing Metadata',
          severity: 'Low',
          reason: 'Mandatory e-invoicing schema fields (UUID / cryptographic stamp) missing from document payload',
          suggestedFix: 'Enrich invoice payload with mandatory UUID, buyer QR code hash, and cryptographic stamp before ERP submission.',
          valueAtRisk: 500, // standard regulatory penalty estimate
          currency: 'AED',
        }
      }
      return null
    },
  },
]

export const indiaRules: ComplianceRule[] = [
  {
    id: 'I1',
    name: 'Gold Sold Without HUID',
    severity: 'High',
    country: 'India',
    description: 'Gold jewellery item sold with no 6-digit alphanumeric HUID.',
    suggestedFix: 'Halt dispatch, route piece to recognized BIS Assaying and Hallmarking Centre (AHC) for laser engraving.',
    evaluate: (inv, config) => {
      if (inv.country !== 'India') return null
      if (config.indiaHuidMandatory && !inv.isSilver && (!inv.huid || inv.huid.trim() === '')) {
        return {
          invoiceId: inv.id,
          date: inv.date,
          store: inv.store,
          country: 'India',
          ruleId: 'I1',
          ruleName: 'Gold Sold Without HUID',
          severity: 'High',
          reason: 'BIS Hallmarking Order mandates 6-digit HUID for all gold jewellery sales in registered districts',
          suggestedFix: 'Halt dispatch, route piece to recognized BIS Assaying and Hallmarking Centre (AHC) for laser engraving.',
          valueAtRisk: inv.totalAmount,
          currency: 'INR',
        }
      }
      return null
    },
  },
  {
    id: 'I2',
    name: 'HUID Unmatched in BIS Register',
    severity: 'High',
    country: 'India',
    description: 'HUID on invoice not found in central BIS hallmark portal register.',
    suggestedFix: 'Verify HUID via BIS Care App API sync or contact AHC center for upload reconciliation.',
    evaluate: (inv, config) => {
      if (inv.country !== 'India') return null
      if (config.indiaHuidMandatory && !inv.isSilver && inv.huid && inv.huidInRegister === false) {
        return {
          invoiceId: inv.id,
          date: inv.date,
          store: inv.store,
          country: 'India',
          ruleId: 'I2',
          ruleName: 'HUID Unmatched in BIS Register',
          severity: 'High',
          reason: `HUID ${inv.huid} failed verification against BIS online portal database`,
          suggestedFix: 'Verify HUID via BIS Care App API sync or contact AHC center for upload reconciliation.',
          valueAtRisk: inv.totalAmount,
          currency: 'INR',
        }
      }
      return null
    },
  },
  {
    id: 'I3',
    name: 'Cash Sale Over TCS Threshold',
    severity: 'Medium',
    country: 'India',
    description: 'Cash sale above configurable threshold flagged for tax collection review.',
    suggestedFix: 'Verify customer PAN/Aadhaar compliance and verify whether TCS under Section 206C(1D) was collected.',
    evaluate: (inv, config) => {
      if (inv.country !== 'India') return null
      if (inv.cashAmount > config.indiaTcsThresholdInr) {
        return {
          invoiceId: inv.id,
          date: inv.date,
          store: inv.store,
          country: 'India',
          ruleId: 'I3',
          ruleName: 'Cash Sale Over TCS Threshold',
          severity: 'Medium',
          reason: `Cash component of ₹${inv.cashAmount.toLocaleString()} exceeds review threshold of ₹${config.indiaTcsThresholdInr.toLocaleString()}`,
          suggestedFix: 'Verify customer PAN/Aadhaar compliance and verify whether TCS under Section 206C(1D) was collected.',
          valueAtRisk: Math.round(inv.cashAmount * 0.01), // 1% TCS liability
          currency: 'INR',
        }
      }
      return null
    },
  },
  {
    id: 'I4',
    name: 'Cash Receipt Exceeds Section 269ST Cap',
    severity: 'High',
    country: 'India',
    description: 'Cash receipt above configurable statutory cap flagged (Income Tax Act Section 269ST).',
    suggestedFix: 'Reverse cash receipt and accept balance payment through banking channels (IMPS/NEFT/Card) to prevent 100% penalty.',
    evaluate: (inv, config) => {
      if (inv.country !== 'India') return null
      if (inv.cashAmount >= config.indiaSection269StCapInr) {
        return {
          invoiceId: inv.id,
          date: inv.date,
          store: inv.store,
          country: 'India',
          ruleId: 'I4',
          ruleName: 'Cash Receipt Exceeds Section 269ST Cap',
          severity: 'High',
          reason: `Cash received (₹${inv.cashAmount.toLocaleString()}) breaches ₹${config.indiaSection269StCapInr.toLocaleString()} ceiling under Section 269ST`,
          suggestedFix: 'Reverse cash receipt and accept balance payment through banking channels (IMPS/NEFT/Card) to prevent 100% penalty.',
          valueAtRisk: inv.cashAmount, // 100% penalty under Section 271DA
          currency: 'INR',
        }
      }
      return null
    },
  },
  {
    id: 'I5',
    name: 'Old Gold Without Margin Tag',
    severity: 'Medium',
    country: 'India',
    description: 'Old gold trade-in transaction recorded without margin-scheme ledger tag.',
    suggestedFix: 'Reclassify purchase in ERP under Rule 32(5) Valuation of Second Hand Goods margin ledger.',
    evaluate: (inv) => {
      if (inv.country !== 'India') return null
      if (inv.isOldGoldTradeIn && inv.marginLedgerTagged === false) {
        return {
          invoiceId: inv.id,
          date: inv.date,
          store: inv.store,
          country: 'India',
          ruleId: 'I5',
          ruleName: 'Old Gold Without Margin Tag',
          severity: 'Medium',
          reason: 'Customer scrap/old-gold trade-in invoice lacks GST margin-scheme ledger allocation',
          suggestedFix: 'Reclassify purchase in ERP under Rule 32(5) Valuation of Second Hand Goods margin ledger.',
          valueAtRisk: Math.round(inv.metalValue * 0.03),
          currency: 'INR',
        }
      }
      return null
    },
  },
  {
    id: 'I6',
    name: 'Silver Hallmark Verification',
    severity: 'Low',
    country: 'India',
    description: 'Silver hallmark check: OFF by default with a toggle, because official guidance was described differently across sources.',
    suggestedFix: 'Verify article purity stamp against voluntary silver hallmarking criteria or toggle rule off if not applicable.',
    evaluate: (inv, config) => {
      if (inv.country !== 'India') return null
      if (config.silverHallmarkCheckEnabled && inv.isSilver && !inv.silverHallmarked) {
        return {
          invoiceId: inv.id,
          date: inv.date,
          store: inv.store,
          country: 'India',
          ruleId: 'I6',
          ruleName: 'Silver Hallmark Verification',
          severity: 'Low',
          reason: 'Silver item lacks hallmarking mark (Note: official guidance described differently across sources).',
          suggestedFix: 'Verify article purity stamp against voluntary silver hallmarking criteria or toggle rule off if not applicable.',
          valueAtRisk: Math.round(inv.totalAmount * 0.05),
          currency: 'INR',
        }
      }
      return null
    },
  },
]

export const allRules: ComplianceRule[] = [...uaeRules, ...indiaRules]

export function scanInvoices(invoices: Invoice[], config: ComplianceConfig): ComplianceException[] {
  const exceptions: ComplianceException[] = []
  for (const inv of invoices) {
    for (const rule of allRules) {
      const exc = rule.evaluate(inv, config)
      if (exc) {
        exceptions.push(exc)
      }
    }
  }
  return exceptions
}
