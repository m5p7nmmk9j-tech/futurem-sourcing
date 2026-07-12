<template>
  <el-select
    :model-value="modelValue"
    :disabled="disabled || !customerId"
    filterable
    clearable
    placeholder="选择进口商资料"
    style="width: 100%"
    @update:model-value="updateValue"
  >
    <el-option
      v-for="item in rows"
      :key="item.id"
      :label="item.isDefault ? `${item.name}（默认）` : item.name"
      :value="item.id"
    >
      <div class="option-row">
        <span>{{ item.name }}</span>
        <small>{{ item.companyName }} · {{ item.taxIdOrRfc || '无 RFC' }}</small>
      </div>
    </el-option>
  </el-select>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { http } from '../api/http'
import type { CustomerImporterProfile } from '../types/orderProduct'

const props = defineProps<{
  customerId?: number | null
  modelValue?: number | null
  disabled?: boolean
}>()

const emit = defineEmits<{
  (event: 'update:modelValue', value: number | null): void
  (event: 'selected', value: CustomerImporterProfile | null): void
}>()

const rows = ref<CustomerImporterProfile[]>([])

async function load() {
  rows.value = []
  if (!props.customerId) return
  const response = await http.get('/customer-importer-profiles', {
    params: { customerId: props.customerId },
  })
  rows.value = response.data || []
  if (!props.modelValue) {
    const defaultRow = rows.value.find(item => item.isDefault && item.status === 'active')
    if (defaultRow) select(defaultRow.id)
  }
}

function select(value: number | null) {
  emit('update:modelValue', value || null)
  emit('selected', rows.value.find(item => item.id === value) || null)
}

function updateValue(rawValue: unknown) {
  select(Number(rawValue || 0) || null)
}

watch(() => props.customerId, load, { immediate: true })
</script>

<style scoped>
.option-row { display: flex; flex-direction: column; line-height: 1.3; }
.option-row small { color: #94a3b8; }
</style>
