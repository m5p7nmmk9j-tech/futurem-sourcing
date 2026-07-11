export function normalizeBarcodeValue(value: unknown): string {
  return String(value ?? '').trim()
}

export function barcodeImageOptions(value: unknown) {
  return {
    format: 'CODE128',
    width: 1.4,
    height: 34,
    margin: 2,
    displayValue: true,
    fontSize: 11,
    textMargin: 1,
  } as const
}
