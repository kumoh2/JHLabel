<h1>Welcome to SvelteKit</h1>
<p>Visit <a href="https://svelte.dev/docs/kit">svelte.dev/docs/kit</a> to read the documentation</p>
<!-- src/routes/+page.svelte -->
<script lang="ts">
    import Sidebar from '$lib/components/Sidebar.svelte';
    import EditorArea from '$lib/components/EditorArea.svelte';
  
    // 임시 더미
    let labels = [
      { id: 1, name: 'Invoice Label' },
      { id: 2, name: 'Shipping Label' },
    ];
    let selectedId: number | null = 1;
  
    // 라벨 사이즈 (mm → px 변환은 나중에 서비스 로직으로)
    const editorWidthPx =  MmToPx(100); // 예시
    const editorHeightPx = MmToPx(50);
  
    function MmToPx(mm: number) {
      // 브라우저 DPI 보정이 필요하면 나중에 구현
      return mm * 4; 
    }
  
    // 이벤트 핸들러 (나중에 서비스 함수 바인딩)
    function handleSelect(id: number) { selectedId = id; }
    function handleNew()       { /* ... */ }
    function handleDelete()    { /* ... */ }
    function handleAddText()   { /* ... */ }
    function handleAdd1D()     { /* ... */ }
    function handleAdd2D()     { /* ... */ }
    function handleSave()      { /* ... */ }
    function handleFront()     { /* ... */ }
    function handleBack()      { /* ... */ }
  </script>
  
  <div class="h-screen grid grid-cols-[1fr_2fr]">
    <Sidebar
      {labels}
      {selectedId}
      onSelect={handleSelect}
      onNew={handleNew}
      onDelete={handleDelete}
      onAddText={handleAddText}
      onAddBarcode1D={handleAdd1D}
      onAddBarcode2D={handleAdd2D}
      onSave={handleSave}
      onBringToFront={handleFront}
      onSendToBack={handleBack}
    />
  
    <div class="p-4 overflow-auto bg-gray-50">
      <EditorArea width={editorWidthPx} height={editorHeightPx}>
        <!-- 여기에 라벨 오브젝트 컴포넌트들이 slot 으로 들어갑니다 -->
      </EditorArea>
    </div>
  </div>
  