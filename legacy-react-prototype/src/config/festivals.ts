export type Festival = {
  name: string
  date: string
  durationDays?: number
}

// Eid shifts about 11 days earlier yearly, so never use a fixed date.
export const festivals: Festival[] = [
  { name: 'Akshaya Tritiya', date: '2024-05-10' },
  { name: 'Akshaya Tritiya', date: '2025-04-30' },
  { name: 'Akshaya Tritiya', date: '2026-04-20' },
  { name: 'Eid al-Fitr', date: '2024-04-10' },
  { name: 'Eid al-Fitr', date: '2025-03-31' },
  { name: 'Eid al-Fitr', date: '2026-03-20' },
  { name: 'Eid al-Adha', date: '2024-06-16' },
  { name: 'Eid al-Adha', date: '2025-06-06' },
  { name: 'Eid al-Adha', date: '2026-05-27' },
  { name: 'Dhanteras', date: '2024-10-29' },
  { name: 'Dhanteras', date: '2025-10-18' },
  { name: 'Dhanteras', date: '2026-11-06' },
  { name: 'Diwali', date: '2024-11-01' },
  { name: 'Diwali', date: '2025-10-20' },
  { name: 'Diwali', date: '2026-11-08' },
  { name: 'Dubai Shopping Festival', date: '2024-12-15', durationDays: 32 },
  { name: 'Dubai Shopping Festival', date: '2025-12-15', durationDays: 32 },
  { name: 'Dubai Shopping Festival', date: '2026-12-15', durationDays: 32 },
]

export const festivalColors: Record<string, string> = {
  Dhanteras: '#d6a84f',
  Diwali: '#f08a66',
  'Akshaya Tritiya': '#6dd5b0',
  'Eid al-Fitr': '#9d8df1',
  'Eid al-Adha': '#65a9de',
  'Dubai Shopping Festival': '#df7fc0',
}
