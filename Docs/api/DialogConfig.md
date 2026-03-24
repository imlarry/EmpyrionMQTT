# Eleon.Modding.DialogConfig Class Reference


## Public Attributes

- **`string TitleText`**
  - by default centered text at top with a little larger font
- **`string BodyText`**
  - by default left-aligned text with standard font size
- **`bool CloseOnLinkClick = true`**
  - If set (default): a click on a text link closes the dialog
- **`string[] ButtonTexts`**
  - Array index: 0 = left, 1 = mid, 2 = right - set to null or empty string if not needed
- **`int ButtonIdxForEsc = -1`**
  - button index that should be "clicked" when ESC key is pressed (default: -1 = not used)
- **`int ButtonIdxForEnter = -1`**
  - dito for Enter key
- **`int MaxChars`**
  - Max allowed chars (default: 0 = don't display input field)
- **`string Placeholder`**
  - text displayed while input field is empty, e.g. "Name?"
- **`string InitialContent`**
  - if set, input field will be pre-filled with it
