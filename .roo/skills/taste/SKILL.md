---
name: taste
description: Build polished, intentional, non-generic frontend interfaces. Use for UI implementation, redesigns, and visual refinement while respecting the existing project design system.
---

# Taste — Anti-Slop Frontend

Before changing UI, inspect the existing project and understand the product, audience, design system, and current components.

## 1. Read the Brief First

Before writing UI code, determine:

- What kind of screen is this?
- Who uses it?
- What is the primary task?
- What visual tone fits the product?
- What existing design system, components, colors, typography, and spacing already exist?
- Are there screenshots, references, or existing pages that should guide the design?

Do not blindly apply a trendy aesthetic.

If the design direction is genuinely ambiguous, ask ONE focused question. Otherwise infer the direction and proceed.

Briefly state the design direction before implementation:

"Design read: <screen/product> for <audience>, using a <visual direction> with <design-system approach>."

## 2. Avoid Generic AI UI

Do NOT automatically produce:

- Purple/blue AI gradients
- Generic glassmorphism
- Three identical feature cards
- Excessive rounded cards
- Excessive shadows
- Random decorative gradients
- Inter + gray + purple as the default
- Excessive animations
- Every section centered
- Decorative elements with no purpose
- Generic dashboard layouts copied from templates

Every visual decision must have a reason related to the product.

## 3. Respect the Existing Design System

Before creating new components:

1. Inspect existing components.
2. Inspect `package.json`.
3. Reuse existing UI primitives.
4. Reuse existing typography, colors, spacing, buttons, forms, and icons.
5. Do not introduce a new design system unnecessarily.

Use ONE coherent visual system throughout the application.

Do not mix unrelated component libraries or icon families.

Never assume a dependency exists. Check `package.json` before importing a third-party library.

## 4. Typography

Typography should establish hierarchy clearly.

Use:
- Strong hierarchy between page title, section title, labels, body text, and metadata.
- Appropriate line height.
- Appropriate letter spacing.
- Readable body text.
- Consistent font choices throughout the application.

Do not use decorative typography just to make a screen look "designed."

Do not default to serif fonts unless the product's visual direction actually calls for them.

## 5. Color

Use a deliberate palette.

Rules:

- Keep the number of accent colors controlled.
- Do not randomly introduce colors for individual components.
- Keep backgrounds, text, borders, states, and accents visually consistent.
- Do not default to purple/blue gradients.
- Use color to communicate hierarchy, status, interaction, and meaning.

For PMP, status colors should have consistent meaning across the application.

Example:

- Success → consistent success treatment
- Warning → consistent warning treatment
- Error → consistent error treatment
- Informational → consistent informational treatment

Do not assign arbitrary colors to statuses.

## 6. Layout

Avoid repetitive "everything in cards" design.

Use:
- Whitespace
- Grouping
- Borders
- Dividers
- Grid
- Flexbox
- Clear hierarchy

Cards should be used when elevation or grouping communicates real hierarchy.

Do not wrap every piece of content in a card.

Prefer CSS Grid for structured layouts rather than complicated flexbox width calculations.

Maintain consistent:
- Page margins
- Content widths
- Spacing scale
- Alignment
- Component heights
- Border radius

## 7. Visual Consistency

Choose a consistent shape language.

For example:

- Consistent border radius
- Consistent button shapes
- Consistent input shapes
- Consistent card treatment
- Consistent shadows
- Consistent border treatment

Do not mix random sharp, rounded, pill-shaped, and heavily rounded components without a deliberate design rule.

## 8. Interaction States

Never design only the successful/default state.

Consider:

- Loading
- Empty
- Error
- Disabled
- Hover
- Focus
- Active
- Validation
- Permission-restricted states

Forms must clearly communicate validation errors.

Loading states should preserve the layout rather than causing major layout shifts.

Empty states should explain what the user can do next.

Errors should be clear and actionable.

## 9. Accessibility

UI must remain usable and accessible.

Check:

- Text/background contrast
- Button contrast
- Form label visibility
- Keyboard focus
- Disabled states
- Error visibility
- Touch target size
- Responsive behavior

Never use placeholder text as the only form label.

Form labels should appear above or alongside their inputs.

## 10. Responsive Design

Design for:

- Desktop
- Tablet
- Mobile

Do not simply shrink the desktop layout.

Check:
- Navigation
- Tables
- Forms
- Cards
- Buttons
- Modals
- Sidebars
- Long text
- Empty/error states

Avoid horizontal overflow.

## 11. Animation

Use motion only when it improves:

- Feedback
- Hierarchy
- Orientation
- State transitions
- Perceived responsiveness

Do not animate everything.

Avoid excessive:
- Parallax
- Floating elements
- Infinite animations
- Large entrance animations
- Decorative motion

Respect `prefers-reduced-motion`.

## 12. Icons

Use an existing icon library already present in the project.

Do not manually draw SVG icons unless absolutely necessary.

Use one coherent icon family throughout the application.

Icons should communicate meaning, not merely decorate empty space.

## 13. PMP-Specific UI Principles

The Property Management Platform is a SaaS product, not a marketing landing page.

Prioritize:

- Clarity
- Trust
- Professionalism
- Efficient workflows
- Information hierarchy
- Consistency
- Accessibility
- Responsive behavior

For dashboards and management screens:

- Do not prioritize visual novelty over usability.
- Do not hide important information behind unnecessary interactions.
- Make primary actions obvious.
- Keep data density appropriate to the user's role.
- Use tables when tabular data is genuinely easier to scan.
- Use cards when they communicate meaningful grouping.
- Make statuses immediately understandable.
- Keep navigation consistent across modules.

Different roles may need different information density:

- Resident → simpler, task-oriented UI
- Property Manager → higher information density
- Technician → action/workflow-oriented UI
- Administrator → management and configuration-oriented UI

## 14. Before Shipping

Perform a visual pre-flight:

### Layout
- Is spacing consistent?
- Is alignment intentional?
- Is the hierarchy obvious?
- Is anything unnecessarily wrapped in a card?

### Typography
- Are headings and body text clearly differentiated?
- Are line lengths readable?
- Are fonts consistent?

### Color
- Is the palette coherent?
- Are status colors consistent?
- Are contrast requirements satisfied?

### Components
- Are existing components reused?
- Are buttons and inputs consistent?
- Are border radii and shadows consistent?
- Are icons from the same family?

### States
- Loading?
- Empty?
- Error?
- Disabled?
- Hover?
- Focus?
- Validation?

### Responsive
- Desktop?
- Tablet?
- Mobile?
- No unwanted horizontal scrolling?

### Final quality check

Ask:

"Does this look intentionally designed for this product, or does it look like a generic AI-generated interface?"

If it looks generic, refine the design before considering the UI complete.