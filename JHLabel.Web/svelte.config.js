import adapterAuto from '@sveltejs/adapter-auto';
import adapterStatic from '@sveltejs/adapter-static';
import { vitePreprocess } from '@sveltejs/vite-plugin-svelte';

const isElectron = process.env.MODE === 'electron';

/** @type {import('@sveltejs/kit').Config} */
const config = {
  preprocess: vitePreprocess(),

  kit: {
    adapter: isElectron ? adapterStatic() : adapterAuto(),
    prerender: {
      entries: isElectron ? ['*'] : []
    }
  }
};

export default config;
