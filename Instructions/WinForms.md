# 🖥️ WINFORMS UI RULES

## 📐 LAYOUT & DESIGN
1. **No Absolute Coordinates**: NEVER use absolute `Location` or `Size` properties for code-generated UI elements. 
2. **Dynamic Containers**: ALWAYS use `TableLayoutPanel` for grid-based layouts and `FlowLayoutPanel` for lists or sequential controls.
3. **Spacing**: Use `Dock`, `Anchor`, `Padding`, and `Margin` exclusively to manage control placement and spacing.
4. **Code-First UI**: Initialize all UI logic and control creation in the main `.cs` class. Avoid touching `Designer.cs` whenever possible.

## 🎨 STYLING
1. **Theming**: Use the `ThemeManager` (if available) to apply consistent colors and styles across controls.
2. **Standard Controls**: Prefer standard WinForms controls but customize them via properties rather than custom drawing unless necessary.
