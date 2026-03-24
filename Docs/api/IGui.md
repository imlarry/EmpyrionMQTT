# Eleon.Modding.IGui Interface Reference


## Public Member Functions

- **`void ShowGameMessage (string text, int prio=0, float duration=3)`**
  - Show popup message (top right of screen), prio: 0 = low, 1 = medium, 2 = high, duration: display duration in seconds
- **`bool ShowDialog (DialogConfig config, DialogActionHandler handler, int customValue)`**
  - Show dialog box with scrollable text area (text can contain clickable links (see http://digitalnativestudios.com/textmeshpro/docs/rich-text/#link), an optional input field and optionally up to three buttons. Returns false if dialog couldn't be displayed (e.g. because it's already open).

## Properties

- **`bool IsWorldVisible [get]`**
