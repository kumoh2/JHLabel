import { defineConfig } from 'vite';
import { sveltekit } from '@sveltejs/kit/vite';
import adapterAuto from '@sveltejs/adapter-auto';
import adapterStatic from '@sveltejs/adapter-static';

const isElectron = process.env.MODE === 'electron';

export default defineConfig({
  plugins: [sveltekit()],
  kit: {
    adapter: isElectron
      ? adapterStatic()
      : adapterAuto(),
    prerender: {
      entries: isElectron ? ['*'] : [],
    },
  },
});
