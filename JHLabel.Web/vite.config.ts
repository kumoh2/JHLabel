import { defineConfig } from 'vite';
import { sveltekit } from '@sveltejs/kit/vite';

const isElectron = process.env.MODE === 'electron';

export default defineConfig({
  plugins: [sveltekit()],

  },
);
