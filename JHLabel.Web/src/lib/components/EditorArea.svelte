<!-- src/lib/components/EditorArea.svelte -->
<script lang="ts">
    import { onMount } from 'svelte';
    export let width: number;   // px
    export let height: number;  // px
    let container: HTMLDivElement;
  
    onMount(() => {
      // TODO: 여기서 캔버스 초기화, 드래그/드롭 리스너 연결 등
    });
  </script>
  
  <div 
    bind:this={container}
    class="bg-white overflow-auto"
    style="width:{width}px; height:{height}px; position:relative;">
    <!-- label objects will be absolutely positioned children -->
    <slot />
  </div>
  