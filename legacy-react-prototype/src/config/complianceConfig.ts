/**
 * Compliance Engine Configuration
 * 
 * IMPORTANT:
 * Verify against current FTA, BIS and tax notices before real use.
 * Thresholds and statutory rules differ across jurisdictions and change regularly.
 */

export interface ComplianceConfig {
  uaeVatRate: number // Default 5% (0.05) per FTA Federal Decree-Law
  uaeRequireB2BDeclaration: boolean
  uaeEInvoicingEnforced: boolean
  indiaTcsThresholdInr: number // Section 206C(1D) / TCS review threshold (default 200,000 INR)
  indiaSection269StCapInr: number // Section 269ST cash acceptance limit per person/day (200,000 INR)
  indiaHuidMandatory: boolean // BIS mandatory hallmarking
  silverHallmarkCheckEnabled: boolean // OFF by default with a toggle, because official guidance was described differently across sources.
  silverHallmarkGuidanceNote: string
}

export const defaultComplianceConfig: ComplianceConfig = {
  uaeVatRate: 0.05,
  uaeRequireB2BDeclaration: true,
  uaeEInvoicingEnforced: true,
  indiaTcsThresholdInr: 200000,
  indiaSection269StCapInr: 200000,
  indiaHuidMandatory: true,
  silverHallmarkCheckEnabled: false,
  silverHallmarkGuidanceNote: 'Official guidance on mandatory silver hallmarking differs across notifications and voluntary schemes. Keep OFF until local jurisdictional clarity is verified.',
}
