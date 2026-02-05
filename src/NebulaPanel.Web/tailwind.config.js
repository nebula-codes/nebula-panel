/** @type {import('tailwindcss').Config} */
module.exports = {
    darkMode: 'class',
    content: [
        './Components/**/*.razor',
        './Pages/**/*.razor',
        './Shared/**/*.razor',
        './*.razor'
    ],
    theme: {
        extend: {
            colors: {
                'nebula': {
                    // Background Layers
                    'bg-void': 'var(--nebula-bg-void)',
                    'bg-primary': 'var(--nebula-bg-primary)',
                    'bg-secondary': 'var(--nebula-bg-secondary)',
                    'bg-tertiary': 'var(--nebula-bg-tertiary)',
                    'bg-elevated': 'var(--nebula-bg-elevated)',
                    'bg-surface': 'var(--nebula-bg-surface)',

                    // Text Colors
                    'text-primary': 'var(--nebula-text-primary)',
                    'text-secondary': 'var(--nebula-text-secondary)',
                    'text-muted': 'var(--nebula-text-muted)',
                    'text-disabled': 'var(--nebula-text-disabled)',

                    // Border Colors
                    'border': 'var(--nebula-border)',
                    'border-subtle': 'var(--nebula-border-subtle)',
                    'border-strong': 'var(--nebula-border-strong)',
                    'border-accent': 'var(--nebula-border-accent)',

                    // Primary Accent
                    'accent': 'var(--nebula-accent)',
                    'accent-light': 'var(--nebula-accent-light)',
                    'accent-dark': 'var(--nebula-accent-dark)',
                    'accent-hover': 'var(--nebula-accent-hover)',
                    'accent-subtle': 'var(--nebula-accent-subtle)',
                    'accent-muted': 'var(--nebula-accent-muted)',

                    // Secondary Accent
                    'secondary': 'var(--nebula-secondary)',
                    'secondary-light': 'var(--nebula-secondary-light)',
                    'secondary-dark': 'var(--nebula-secondary-dark)',
                    'secondary-subtle': 'var(--nebula-secondary-subtle)',

                    // Status Colors
                    'success': 'var(--nebula-success)',
                    'success-light': 'var(--nebula-success-light)',
                    'success-dark': 'var(--nebula-success-dark)',
                    'success-subtle': 'var(--nebula-success-subtle)',
                    'warning': 'var(--nebula-warning)',
                    'warning-light': 'var(--nebula-warning-light)',
                    'warning-dark': 'var(--nebula-warning-dark)',
                    'warning-subtle': 'var(--nebula-warning-subtle)',
                    'error': 'var(--nebula-error)',
                    'error-light': 'var(--nebula-error-light)',
                    'error-dark': 'var(--nebula-error-dark)',
                    'error-subtle': 'var(--nebula-error-subtle)',
                    'info': 'var(--nebula-info)',
                    'info-light': 'var(--nebula-info-light)',
                    'info-dark': 'var(--nebula-info-dark)',
                    'info-subtle': 'var(--nebula-info-subtle)',

                    // Component-Specific
                    'sidebar-bg': 'var(--nebula-sidebar-bg)',
                    'card-bg': 'var(--nebula-card-bg)',
                    'input-bg': 'var(--nebula-input-bg)',
                    'input-bg-hover': 'var(--nebula-input-bg-hover)',
                    'console-bg': 'var(--nebula-console-bg)',
                    'overlay-bg': 'var(--nebula-overlay-bg)',

                    // Glass Effect
                    'glass-bg': 'var(--nebula-glass-bg)',
                    'glass-border': 'var(--nebula-glass-border)',
                }
            },
            fontFamily: {
                'display': ['var(--nebula-font-display)', 'system-ui', 'sans-serif'],
                'body': ['var(--nebula-font-body)', 'system-ui', 'sans-serif'],
                'sans': ['var(--nebula-font-body)', 'system-ui', 'sans-serif'],
                'mono': ['var(--nebula-font-mono)', 'monospace'],
            },
            fontSize: {
                'display': ['var(--nebula-text-display)', { lineHeight: 'var(--nebula-leading-display)', letterSpacing: 'var(--nebula-tracking-tight)' }],
                'h1': ['var(--nebula-text-h1)', { lineHeight: 'var(--nebula-leading-heading)', letterSpacing: 'var(--nebula-tracking-tight)' }],
                'h2': ['var(--nebula-text-h2)', { lineHeight: 'var(--nebula-leading-heading)', letterSpacing: 'var(--nebula-tracking-tight)' }],
                'h3': ['var(--nebula-text-h3)', { lineHeight: 'var(--nebula-leading-heading)' }],
                'h4': ['var(--nebula-text-h4)', { lineHeight: 'var(--nebula-leading-heading)' }],
                'body': ['var(--nebula-text-body)', { lineHeight: 'var(--nebula-leading-body)' }],
                'small': ['var(--nebula-text-small)', { lineHeight: 'var(--nebula-leading-body)' }],
                'caption': ['var(--nebula-text-caption)', { lineHeight: 'var(--nebula-leading-tight)' }],
            },
            spacing: {
                'space-1': 'var(--space-1)',
                'space-2': 'var(--space-2)',
                'space-3': 'var(--space-3)',
                'space-4': 'var(--space-4)',
                'space-5': 'var(--space-5)',
                'space-6': 'var(--space-6)',
                'space-7': 'var(--space-7)',
                'space-8': 'var(--space-8)',
                'space-9': 'var(--space-9)',
                'space-10': 'var(--space-10)',
            },
            boxShadow: {
                'nebula-sm': 'var(--nebula-shadow-sm)',
                'nebula': 'var(--nebula-shadow)',
                'nebula-lg': 'var(--nebula-shadow-lg)',
                'nebula-xl': 'var(--nebula-shadow-xl)',
                'nebula-glow': 'var(--nebula-glow)',
                'nebula-glow-intense': 'var(--nebula-glow-intense)',
                'nebula-glow-secondary': 'var(--nebula-glow-secondary)',
                'nebula-glow-success': 'var(--nebula-glow-success)',
                'nebula-glow-error': 'var(--nebula-glow-error)',
            },
            borderRadius: {
                'nebula-sm': 'var(--nebula-radius-sm)',
                'nebula': 'var(--nebula-radius)',
                'nebula-lg': 'var(--nebula-radius-lg)',
                'nebula-xl': 'var(--nebula-radius-xl)',
            },
            backgroundImage: {
                'nebula-radial': 'var(--nebula-gradient-radial)',
                'nebula-subtle': 'var(--nebula-gradient-subtle)',
                'nebula-accent': 'var(--nebula-gradient-accent)',
                'nebula-shine': 'var(--nebula-gradient-shine)',
                'nebula-gradient': 'var(--nebula-gradient)',
                'nebula-gradient-subtle': 'var(--nebula-gradient-subtle)',
            },
            backdropBlur: {
                'nebula': '12px',
                'nebula-strong': '20px',
            },
            animation: {
                'spin-slow': 'spin 2s linear infinite',
                'pulse-glow': 'nebula-pulse-glow 2s ease-in-out infinite',
                'fade-in': 'nebula-fade-in 0.2s ease-out',
                'slide-in': 'nebula-slide-in 0.2s ease-out',
                'slide-up': 'nebula-slide-up 0.3s ease-out',
                'twinkle': 'twinkle 8s ease-in-out infinite',
                'badge-pulse': 'badge-pulse 2s ease-in-out infinite',
                'modal-scale': 'modal-scale-in 0.2s ease-out',
                'modal-slide': 'modal-slide-in 0.25s ease-out',
                'select-open': 'select-open 0.15s ease-out',
            },
            keyframes: {
                'nebula-pulse-glow': {
                    '0%, 100%': { boxShadow: '0 0 15px var(--nebula-accent-glow)' },
                    '50%': { boxShadow: '0 0 30px var(--nebula-accent-glow-intense)' },
                },
                'nebula-fade-in': {
                    '0%': { opacity: '0' },
                    '100%': { opacity: '1' },
                },
                'nebula-slide-in': {
                    '0%': { opacity: '0', transform: 'translateY(-10px)' },
                    '100%': { opacity: '1', transform: 'translateY(0)' },
                },
                'nebula-slide-up': {
                    '0%': { opacity: '0', transform: 'translateY(10px)' },
                    '100%': { opacity: '1', transform: 'translateY(0)' },
                },
                'twinkle': {
                    '0%, 100%': { opacity: '0.6' },
                    '50%': { opacity: '0.3' },
                },
                'badge-pulse': {
                    '0%, 100%': { opacity: '1' },
                    '50%': { opacity: '0.7' },
                },
                'modal-scale-in': {
                    '0%': { opacity: '0', transform: 'scale(0.95)' },
                    '100%': { opacity: '1', transform: 'scale(1)' },
                },
                'modal-slide-in': {
                    '0%': { opacity: '0', transform: 'translateY(20px)' },
                    '100%': { opacity: '1', transform: 'translateY(0)' },
                },
                'select-open': {
                    '0%': { opacity: '0', transform: 'translateY(-4px)' },
                    '100%': { opacity: '1', transform: 'translateY(0)' },
                },
            },
        }
    },
    plugins: [
        require('@tailwindcss/forms'),
        require('@tailwindcss/typography'),
    ]
};
