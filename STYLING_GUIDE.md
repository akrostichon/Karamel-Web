# Karamel Web App – Modern Style Guide

## Document Purpose
This document defines the **visual design, color logic, typography, component states, and implementation guidance** for the **Karamel entertainment web app**.  
The design aims to feel **modern, vibrant, luxurious** while maintaining clarity and performance.  
It supports **light and dark modes** with consistent inverse color logic.

---

### Note to Developers
This guide reflects the concept direction for a **modern Blazor‑inspired web app**.  
If components or UX patterns differ, adapt color tokens while preserving the identity:  
**clean layout • dynamic gradients • rich accents • high readability.**

---

## 1. Brand Concept
**Essence:** Warm caramel light meets social karaoke energy.  
**Mood:** Playful • luxurious • vibrant • accessible.  
**Logo Cue:** Rounded golden microphone glowing with amber light.  
**Visual Character:** Semi‑flat surfaces, soft shadows, luminous accents, open spacing.

---

## 2. Color System Overview

### 2.1 Design Principle
A neutral foundation with a luminous amber accent palette.  
Light ⇄ dark modes invert the neutral values while accents stay consistent.

### 2.2 Neutral Base Palette

| Tone | Light Mode | Dark Mode | Use |
|:--|:--|:--|:--|
| Background Base | #F3F0EE | #1E1B1A | Main background |
| Surface / Card | #FFFFFF | #292423 | Cards and containers |
| Outline / Border | #D4C9C3 | #3F3936 | Card separation / component edges |

### 2.3 Accent Palette

| Role | Light Mode | Dark Mode | Usage |
|:--|:--|:--|:--|
| Primary Accent (Golden Amber) | #FFA733 | #FFB64C | Primary buttons / interactive focus |
| Secondary Accent (Hot Caramel) | #DB5F26 | #FF8040 | Hover states / active icons |
| Highlight Glow | #FFD25F | #FFC254 | Focus rings / decorative glow |
| Error / Warning | #E64848 | #FF6E6E | Destructive actions |
| Success / Confirm | #45D483 | #50E89A | Positive state / confirmation |
| Link / Accent Text | #E59133 | #FFC052 | Interactive text / CTA links |

### 2.4 Typography Contrast Colors

| Use | Light Mode | Dark Mode |
|:--|:--|:--|
| Primary Text | #1C1A19 | #F3F1ED |
| Secondary Text | #5B554E | #C0B8B0 |
| Disabled Text | #BEB8B3 | #5C5754 |

---

## 3. Gradient Accents

| Gradient Name | Colors | Typical Use |
|:--|:--|:--|
| Karamel Glow | #FFB347 → #FF9447 | Buttons, icon illumination |
| Stage Depth | #FF8A33 → #9B3F1E | Top bar / header |
| Night Lava | #2A0500 → #702626 | Dark‑mode app background |

---

## 4. Typography System

| Type | Font Family | Weight | Size | Common Use |
|:--|:--|:--|:--|:--|
| Logo / Display | Poppins / Baloo | Bold–ExtraBold | 32–48 px | Brand / Headers |
| Headings | Nunito / Poppins | SemiBold–Bold | 20–32 px | Section titles |
| Body | Nunito / Lato | Regular–Medium | 14–16 px | Main text |
| Caption / Meta | Nunito / Lato | Regular | 12–13 px | Small labels |

*Guidelines:*  
Use relative units (rem / clamp) for scaling on mobile and desktop.  
Line‑height ≈ 1.5 for readability.
Favor rounded sans‑serifs to match the soft branding.

---

## 5. Layout Framework

### Grid System

| Screen | Max Width | Columns | Gutter | Margins |
|:--|:--|:--|:--|:--|
| Mobile | ≤ 480 px | 4 | 16 px | 16 px |
| Tablet | 481–1024 px | 8 | 24 px | 24 px |
| Desktop | > 1024 px | 12 | 32 px | 64 px |

**Spacing:** 8‑pt grid (8, 16, 24 px increments).  
**Corner Radius:** Cards 8–12 px ; Buttons 6–10 px.  
**Shadows:** RGBA(0, 0, 0, 0.15) subtle for depth.

---

## 6. Component Styles

### Buttons
- Primary: Amber gradient fill, dark text in light mode; light text in dark mode.  
- Hover: Brighten gradient around edges (+6 % lightness).  
- Active: Slight scale 0.98 for press feedback.  
- Disabled: Opacity 0.5 ; no shadow.  
- Focus: Glow using Highlight Glow color.

### Input Fields
- Border light: #E3B274; border dark: #3A241B.  
- Focus: Amber outline.  
- Padding: 12 px vertical / 16 px horizontal.  
- Font color adapts to mode contrast.

### Cards
- Light: #FFFFFF with soft shadow.  
- Dark: #292423 with border #3F3936.  
- Radius 16 px.  
- Elevation Level 1 = shadow 2 px 5 px 7 px RGBA(0, 0, 0, 0.12).

### Tables / Lists
- Alternating row shading: background tone ± 3 %.  
- Selected row: overlay Amber accent at 20 % opacity.  
- Search input highlight: Amber underline.

### Navigation
- Desktop: Top sticky bar (“Stage Depth” gradient).  
- Mobile: Bottom tab bar using Primary Accent icons.  
- Active icon glow state in Highlight Glow.

---

## 7. Motion / Interaction Feedback
- Default transition = 200 ms ease‑in‑out.  
- Menu expansions = 250 ms cubic‑bezier(0.4, 0, 0.2, 1).  
- Hover states = color + shadow shift only (no position jump).  
- Video or Full‑Screen mode = UI fades out to neutral overlay 0.2 opacity.

---

## 8. Technical Recommendations
- Store all theme tokens as CSS variables in root (light + dark).  
Example format:  
:root { --color-background:#F3F0EE; --color-accent:#FFA733; }  
@media (prefers-color-scheme:dark) { --color-background:#1E1B1A; }  
- Use CSS Grid / Flexbox for layout.  
- All graphic icons as SVG.  
- Respect system color‑scheme for automatic light/dark switch.  
- Performance target: 60 FPS interaction transitions.  
- Maintain shared tokens between desktop and mobile interfaces.

---

## 9. Accessibility & Usability
- WCAG 2.1 AA contrast recommended (even without strict requirement).  
- Focus outline = 2 px in Highlight Glow color.  
- Touch targets ≥ 48 × 48 px.  
- Keyboard navigation (Tab / Enter) supported.  
- Spacing and contrast tested for bar environments (low light visibility).

---

## 10. Brand Logo Reference
- Concept: Microphone on three caramel cubes.  
- Primary color context: Amber glow on dark base or bright contrast.  
- Safe padding: minimum 1 × icon width clear space.  
- Preferred format: vector SVG / blur‑resistant PNG.  
- Monochrome: #3C2415 for light BGs ; #F5E1C0 for dark BGs.  
- Never invert the amber to cold tones – warm light is core identity.

---

## 11. Summary of Key Design Goals

| Goal | Description |
|:--|:--|
| **Modern Luxury** | Warm amber with neutral surfaces and soft depth. |
| **Readability** | High contrast text on inverted themes. |
| **Consistency** | Unified tokens for desktop and mobile interfaces. |
| **Cross‑Device Harmony** | Seamless theme transfer across screens. |
| **Performance & Clarity** | Minimal shadows, fast animations, clean gradients. |

---

### Author’s Note to Developers and Designers
This guide establishes the modern visual language for Karamel.  
If future implementations introduce new views or interaction patterns, update tokens while retaining the essence:  
luxurious amber accents, neutral bases, smooth lighting gradients, and clear typography.

---

**End of Document**