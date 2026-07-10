<template>
  <div class="barcode-box" v-if="value">
    <svg ref="svgRef" class="barcode-svg"></svg>
  </div>
  <span v-else class="empty">-</span>
</template>

<script setup lang="ts">
import { nextTick, onMounted, ref, watch } from 'vue'
import JsBarcode from 'jsbarcode'

const props = withDefaults(defineProps<{
  value?: string | null
  height?: number
  width?: number
  displayValue?: boolean
}>(), {
  value: '',
  height: 34,
  width: 1.35,
  displayValue: true
})

const svgRef = ref<SVGSVGElement | null>(null)

async function renderBarcode() {
  await nextTick()
  if (!svgRef.value || !props.value) return
  try {
    JsBarcode(svgRef.value, String(props.value), {
      format: 'CODE128',
      height: props.height,
      width: props.width,
      margin: 2,
      fontSize: 11,
      displayValue: props.displayValue,
      background: '#ffffff',
      lineColor: '#111827'
    })
  } catch {
    svgRef.value.innerHTML = ''
  }
}

onMounted(renderBarcode)
watch(() => [props.value, props.height, props.width, props.displayValue], renderBarcode)
</script>

<style scoped>
.barcode-box{display:flex;justify-content:center;align-items:center;min-height:52px;background:#fff;border-radius:4px;overflow:hidden}.barcode-svg{max-width:100%;height:auto}.empty{color:#9ca3af}
</style>
