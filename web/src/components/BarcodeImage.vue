<template>
  <div v-if="barcode" class="barcode-box" :title="barcode">
    <svg ref="svgRef" class="barcode-svg" aria-label="商品条码图片"></svg>
  </div>
  <span v-else class="empty">-</span>
</template>

<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import JsBarcode from 'jsbarcode'
import { barcodeImageOptions, normalizeBarcodeValue } from '../utils/barcodeImage'

const props = withDefaults(defineProps<{
  value?: string | number | null
  height?: number
  width?: number
  displayValue?: boolean
}>(), {
  value: '',
  height: 34,
  width: 1.4,
  displayValue: true,
})

const svgRef = ref<SVGSVGElement | null>(null)
const barcode = computed(() => normalizeBarcodeValue(props.value))

async function renderBarcode() {
  await nextTick()
  if (!svgRef.value || !barcode.value) return

  try {
    svgRef.value.replaceChildren()
    JsBarcode(svgRef.value, barcode.value, {
      ...barcodeImageOptions(barcode.value),
      height: props.height,
      width: props.width,
      displayValue: props.displayValue,
      background: '#ffffff',
      lineColor: '#111827',
    })
  } catch {
    svgRef.value.replaceChildren()
  }
}

watch(() => [barcode.value, props.height, props.width, props.displayValue], renderBarcode, { immediate: true })
</script>

<style scoped>
.barcode-box {
  display: flex;
  min-height: 52px;
  align-items: center;
  justify-content: center;
  overflow: hidden;
  border-radius: 4px;
  background: #fff;
}

.barcode-svg {
  display: block;
  max-width: 100%;
  height: auto;
}

.empty {
  color: #9ca3af;
}
</style>
