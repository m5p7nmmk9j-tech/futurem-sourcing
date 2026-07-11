<template>
  <div class="shipment-measurements">
    <div class="section-title">体积与重量</div>
    <el-row :gutter="12">
      <el-col :span="8">
        <el-form-item label="计算立方数">
          <el-input-number :model-value="Number(shipment.calculatedTotalCbm || 0)" :precision="2" disabled style="width:100%" />
        </el-form-item>
      </el-col>
      <el-col :span="8">
        <el-form-item label="计算毛重(kg)">
          <el-input-number :model-value="Number(shipment.calculatedGrossWeightKg || 0)" :precision="2" disabled style="width:100%" />
        </el-form-item>
      </el-col>
      <el-col :span="8">
        <el-form-item label="计算净重(kg)">
          <el-input-number :model-value="Number(shipment.calculatedNetWeightKg || 0)" :precision="2" disabled style="width:100%" />
        </el-form-item>
      </el-col>
      <el-col :span="8">
        <el-form-item label="最终立方数">
          <el-input-number v-model="shipment.finalTotalCbm" :precision="2" :min="0" style="width:100%" />
        </el-form-item>
      </el-col>
      <el-col :span="8">
        <el-form-item label="最终毛重(kg)">
          <el-input-number v-model="shipment.finalGrossWeightKg" :precision="2" :min="0" style="width:100%" />
        </el-form-item>
      </el-col>
      <el-col :span="8">
        <el-form-item label="最终净重(kg)">
          <el-input-number v-model="shipment.finalNetWeightKg" :precision="2" :min="0" style="width:100%" />
        </el-form-item>
      </el-col>
    </el-row>
    <div class="actions">
      <el-button @click="emit('recalculate', false)">重新计算</el-button>
      <el-button @click="emit('recalculate', true)">重新计算并覆盖最终值</el-button>
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{ shipment: any }>()
const emit = defineEmits<{ (e: 'recalculate', overwrite: boolean): void }>()
</script>

<style scoped>
.shipment-measurements { margin: 12px 0; padding: 14px; border: 1px solid var(--el-border-color); border-radius: 8px; }
.section-title { margin-bottom: 12px; font-weight: 700; }
.actions { display: flex; justify-content: flex-end; gap: 8px; }
</style>
