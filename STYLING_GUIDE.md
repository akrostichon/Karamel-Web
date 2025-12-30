# Karamel Web App — Visual Style & UI Design Guide

## Document Purpose
This document defines the **visual styling, colors, typography, component behavior, and layout principles** for the **Karamel Karaoke Web App**.  
It summarizes all design discussions up to this point to provide consistent implementation guidance.

---

### Note to Developer / Coding Agent
These specifications are based on conceptual and functional assumptions provided during the design phase.  
At the time of writing, details about **view hierarchy, feature set, or page structure** were partially unknown.  
If any specifications conflict with the actual implementation (e.g., *specific views, screen flows, or component structures*), please **overwrite or adapt** these decisions as appropriate.  
Clarity, usability, and consistency have priority over literal adherence to this document.

---

## 1. Brand Concept

**Product Name:** Karamel  
**Theme Origin:** Wordplay between *Karaoke* and *Caramel*  
**Visual Essence:**  
Warm, welcoming, and fun — merging the **sweetness of caramel** with the **energy of a karaoke performance**.  
Primary motif: **microphone in caramel tones**, sometimes resting on caramel cubes, set against a calm **warm red** background.

---

## 2. Color System

### Base Palette

| Role | Color Name | HEX | Usage |
|------|-------------|-----|-------|
| Primary | Caramel Gold | #EAAE63 | Main brand & CTA color |
| Primary Dark | Toffee Brown | #8B5A2B | Hover states, depth tone |
| Accent / Brand Red | Warm Red | #B23A48 | Energetic branding highlight |
| Neutral Light | Cream Beige | #F5E1C0 | Backgrounds & panels |
| Neutral Dark | Roasted Mocha | #3C2415 | Text & dark backgrounds |
| Highlight | Golden Honey | #FFC878 | Active states, glows |
| Success | Soft Mint Green | #A7D8A5 | Positive feedback |
| Error | Muted Rose Red | #C76C6C | Error or warning |

### Light and Dark Mode Variants

| Role | Light Mode | Dark Mode |
|------|-------------|-----------|
| App Background | #FFF8F2 | #2C1B13 |
| Surface / Cards | #FFEBD8 | #3A241B |
| Text Primary | #3C2415 | #F5E1C0 |
| Text Secondary | #8B5A2B | #D9B998 |
| Accent | #B23A48 | #E56A77 |
| Button Primary | #EAAE63 text #3C2415 | #CB8F53 text #F5E1C0 |

Maintain **contrast ≥ 4.5:1** for WCAG AA compliance.

---

## 3. Typography

| Type | Font Family | Weight | Size Range | Typical Use |
|------|--------------|---------|-------------|--------------|
| Display / Logo | Poppins / Baloo | Bold–ExtraBold | 32–48 px (desktop), 24–32 px (mobile) | Branding, titles |
| Headings (H1–H2) | Nunito / Poppins | SemiBold–Bold | 20–32 px responsive | Section titles |
| Body Text | Nunito / Lato | Regular–Medium | 14–16 px | Paragraphs, labels |
| Caption / Metadata | Nunito / Lato | Regular | 12–13 px | Small hints, timestamps |

Use `rem` or `clamp()` for fluid scaling.

---

## 4. Layout Framework (Web Context)

### Grid System

| Screen Type | Max Width | Columns | Gutter | Margin |
|--------------|------------|----------|---------|---------|
| Mobile | ≤ 480 px | 4 | 16 px | 16 px |
| Tablet | 481–1024 px | 8 | 24 px | 24 px |
| Desktop | > 1024 px | 12 | 32 px | 64 px |

### Spacing
Base unit: **8 px** (8 pt grid for margins, paddings, icons).

---

## 5. Cross‑Device Usage Model

### Desktop (Main Screen)
**Purpose:** Stage display (lyrics, visuals, controllers).  
**Layout:**
- Header: app logo + song title (background #B23A48, text #F5E1C0)
- Main: lyrics/duet visuals on gradient (#EAAE63 → #CF884A)
- Sidebar: singer queue (cards on #FFEBD8)
- Footer: connection / QR / small settings

**Design:**
- Bold fonts 32–48 px
- Spaced, readable visuals
- Target resolution 1920×1080 px

### Smartphones (Side Views)
**Purpose:** Companion control interfaces.  

Possible views:
- Controller: play/stop/volume
- Queue: song voting / management
- Lyrics: synchronized line display
- Profile: user info & scores

**Principles:**
- Bottom navigation (3–4 icons)
- Thumb‑reach placement for CTAs
- Typography 14–18 px, touch areas ≥ 48 px
- Shared color palette & animation scheme

**Synchronization:**  
Mobile actions trigger highlights on desktop (Caramel Gold / Golden Honey tone).

---

## 6. Components and States

### Buttons

| State | Style | Behavior |
|--------|--------|-----------|
| Default | Primary (Caramel Gold) | Rounded corners, soft shadow |
| Hover / Active | Slightly darker tone | Smooth transition 0.2 s |
| Disabled | 50 % opacity | Non‑interactive |
| Secondary | Outline Toffee Brown | Transparent background |

### Input Fields
- Border #E3B274 (light) / #3A241B (dark)  
- Focus ring #FFC878 (glow)  
- Radius 8 px

### Cards / Panels
- Light #FFEBD8 or Dark #3A241B  
- Corner radius 16 px, shadow `rgba(0,0,0,0.15)`

### Navigation
- Desktop: top sticky translucent red bar  
- Mobile: bottom tab bar with caramel icons

---

## 7. Gradients & Effects

| Gradient Name | Colors | Use |
|----------------|--------|-----|
| Caramel Flow | #EAAE63 → #CF884A | CTA backgrounds |
| Sweet Red | #B23A48 → #E56A77 | Splash/header |
| Honey Light | #FFC878 → transparent | Hover glows |

---

## 8. Technical Recommendations
- Define CSS variables for tokens: colors, fonts, spacing.  
- Support `prefers-color-scheme: dark`.  
- Use SVG icons.  
- Optimize for GPU performance and transitions (60 FPS).  
- Utilize `flex`/`grid` for layout.

---

## 9. Accessibility & Usability
- Follow **WCAG 2.1 AA**.  
- Focus outline: 2 px Golden Honey.  
- Adequate touch & click sizes.  
- Contrast checks for text on red backgrounds.  

---

## 10. Brand Logo Reference

**Structure:** Microphone resting on three caramel cubes.  
**Colors:** Caramel tones on warm red #B23A48.  
**Safe spacing:** ≥ 1× icon width padding.  
**Monochrome:**
- Dark background → #F5E1C0  
- Light background → #3C2415  

---

## 11. Summary of Key Design Goals

| Goal | Description |
|------|--------------|
| Warm & inviting | Caramel and red palette |
| Readable & clear | Rounded sans fonts, large displays |
| Responsive & consistent | Shared design tokens |
| Cross‑device usability | Coordinated desktop / mobile roles |
| Flexible implementation | Editable tokens and adaptable colors |

---

### Author’s Note to Coding Agent
This styling guide encapsulates conceptual design decisions made with limited knowledge of the final page structure and feature breakdown.  
If implementation details differ (e.g., separate component views, different navigation logic), please **adjust or overwrite** the design tokens, layout rules, or color assignments.  
Consistency, accessibility, and responsiveness remain the central principles for any modifications.

---

**End of Document**