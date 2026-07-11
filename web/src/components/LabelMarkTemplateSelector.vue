<template>
  <el-row :gutter="12">
    <el-col :span="12">
      <el-select
        :model-value="labelTemplateId"
        :disabled="disabled || !customerId"
        filterable
        clearable
        placeholder="选择商品标签模板"
        style="width: 100%"
        @update:model-value="value => select('label', value)"
      >
        <el-option
          v-for="item in labels"
          :key="item.id"
          :label="item.isDefault ? `${item.name}（默认）` : item.name"
          :value="item.id"
        />
      </el-select>
    </el-col>
    <el-col :span="12">
      <el-select
        :model-value="markTemplateId"
        :disabled="disabled || !customerId"
        filterable
        clearable
        placeholder="选择外箱唛头模板"
        style="width: 100%"
        @update:model-value="value => select('mark', value)"
      >
        <el-option
          v-for="item in marks"
          :key="item.id"
          :label="item.isDefault ? `${item.name}（默认）` : item.name"
          :value="item.id"
        />
      </el-select>
    </el-col>
  </el-row>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { http } from '../api/http'
import type { LabelMarkTemplate } from '../types/orderProduct'

const props = defineProps<{
  customerId?: number | null
  labelTemplateId?: number | null
  markTemplateId?: number | null
  disabled?: boolean
}>()

const emit = defineEmits<{
  (event: 'update:labelTemplateId', value: number | null): void
  (event: 'update:markTemplateId', value: number | null): void
}>()

const labels = ref<LabelMarkTemplate[]>([])
const marks = ref<LabelMarkTemplate[]>([])

async function load() {
  labels.value = []
  marks.value = []
  if (!props.customerId) return
  const [labelResponse, markResponse] = await Promise.all([
    http.get('/label-mark-templates', { params: { customerId: props.customerId, templateType: 'product_label' } }),
    http.get('/label-mark-templates', { params: { customerId: props.customerId, templateType: 'carton_mark' } }),
  ])
  labels.value = labelResponse.data || []
  marks.value = markResponse.data || []

  if (!props.labelTemplateId) {
    const item = labels.value.find(row => row.isDefault) || labels.value[0]
    if (item) emit('update:labelTemplateId', item.id)
  }
  if (!props.markTemplateId) {
    const item = marks.value.find(row => row.isDefault) || marks.value[0]
    if (item) emit('update:markTemplateId', item.id)
  }
}

function select(type: 'label' | 'mark', rawValue: unknown) {
  const value = Number(rawValue || 0) || null
  if (type === 'label') emit('update:labelTemplateId', value)
  else emit('update:markTemplateId', value)
}

watch(() => props.customerId, load, { immediate: true })
</script>
