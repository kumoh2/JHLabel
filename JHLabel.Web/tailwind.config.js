// tailwind.config.cjs
/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './src/**/*.{html,js,ts,svelte,css}'
  ],
  theme: {
    extend: {
      // 필요한 커스텀 테마가 있다면 여기에
    }
  },
  plugins: [
    // 예: require('@tailwindcss/forms'),
  ]
};
