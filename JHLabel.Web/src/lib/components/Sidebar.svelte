<!-- src/lib/components/Sidebar.svelte -->
<script lang="ts">
  export let labels: { id: number; name: string }[] = [];
  export let selectedId: number | null = null;
  export let onSelect: (id: number) => void;
  export let onNew: () => void;
  export let onDelete: () => void;
  export let onAddText: () => void;
  export let onAddBarcode1D: () => void;
  export let onAddBarcode2D: () => void;
  export let onSave: () => void;
  export let onBringToFront: () => void;  
  export let onSendToBack: () => void;
</script>

<div class="flex flex-col h-full bg-white border-r">
  <!-- Label List -->
  <div class="flex-1 overflow-auto p-2">
    <ul>
      {#each labels as lbl}
        <li class="mb-1">
          <button
            type="button"
            class="w-full text-left cursor-pointer px-3 py-2 rounded hover:bg-indigo-50"
            class:bg-indigo-100={lbl.id === selectedId}
            class:font-semibold={lbl.id === selectedId}
            on:click={() => onSelect(lbl.id)}
          >
            {lbl.name}
          </button>
        </li>
      {/each}
    </ul>
  </div>

  <!-- Buttons -->
  <div class="p-4 space-y-2 border-t">
    <button class="w-full btn-primary"   on:click={onNew}>New Label</button>
    <button class="w-full btn-danger"    on:click={onDelete}>Delete Label</button>
    <button class="w-full btn-secondary" on:click={onAddText}>Add Text</button>
    <button class="w-full btn-secondary" on:click={onAddBarcode1D}>Add 1D Barcode</button>
    <button class="w-full btn-secondary" on:click={onAddBarcode2D}>Add 2D Barcode</button>
    <button class="w-full btn-primary"   on:click={onSave}>Save Label</button>
    <button class="w-full btn-secondary" on:click={onBringToFront}>Bring to Front</button>
    <button class="w-full btn-secondary" on:click={onSendToBack}>Send to Back</button>
  </div>
</div>
