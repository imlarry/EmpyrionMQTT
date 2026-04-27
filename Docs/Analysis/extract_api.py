"""
Doxygen HTML -> Markdown extractor for Empyrion Modding API docs.
Produces one .md file per interface/class/struct in Modding Doc/api/
"""
import os
import re
import html
from html.parser import HTMLParser

SRC_DIR = os.path.join(os.path.dirname(__file__), "html")
OUT_DIR = os.path.join(os.path.dirname(__file__), "api")

HTML_ENTITY = re.compile(r'&#\d+;|&[a-z]+;')
MULTI_SPACE  = re.compile(r'[ \t]+')
MULTI_NL     = re.compile(r'\n{3,}')


def clean(text):
    text = html.unescape(text)
    text = MULTI_SPACE.sub(' ', text)
    return text.strip()


class DoxygenParser(HTMLParser):
    def __init__(self):
        super().__init__()
        self.reset_state()

    def reset_state(self):
        self.title        = ""
        self.inherits     = ""
        self.sections     = []        # list of {heading, members:[{type,sig,desc}]}
        self.source_file  = ""

        self._cur_section  = None
        self._cur_member   = None     # {type, sig, desc}
        self._in_title     = False
        self._in_left      = False
        self._in_right     = False
        self._in_desc      = False
        self._in_memdoc    = False
        self._in_heading   = False
        self._in_inherit   = False
        self._depth_memdoc = 0
        self._tag_stack    = []
        self._buf          = ""
        self._skip_tags    = {"script", "style", "img"}
        self._skip_depth   = 0

    # ------------------------------------------------------------------ helpers
    def _flush(self):
        t, self._buf = self._buf, ""
        return clean(t)

    def _push_section(self):
        # flush pending member into current section before rotating
        if self._cur_member is not None and self._cur_section is not None:
            if self._cur_member.get("sig") or self._cur_member.get("type"):
                self._cur_section["members"].append(self._cur_member)
            self._cur_member = None
        if self._cur_section is not None:
            self.sections.append(self._cur_section)
        self._cur_section = {"heading": "", "members": []}

    def _push_member(self):
        if self._cur_member and self._cur_section is not None:
            self._cur_section["members"].append(self._cur_member)
        self._cur_member = {"type": "", "sig": "", "desc": ""}

    # ------------------------------------------------------------------ parser
    def handle_starttag(self, tag, attrs):
        self._tag_stack.append(tag)
        attrs = dict(attrs)
        cls   = attrs.get("class", "")

        if self._skip_depth:
            self._skip_depth += 1
            return
        if tag in self._skip_tags:
            self._skip_depth = 1
            return

        # Title
        if tag == "div" and cls == "title":
            self._in_title = True

        # Inheritance line
        elif tag == "p" and self._cur_section is None:
            # first <p> before any section = inherit line
            self._in_inherit = True

        # Section heading
        elif tag == "tr" and cls == "heading":
            self._push_section()
            self._in_heading = True

        # Member declaration row
        elif tag == "tr" and cls.startswith("memitem:"):
            self._push_member()

        # Member type (left cell)
        elif tag == "td" and cls == "memItemLeft":
            self._in_left = True

        # Member signature (right cell)
        elif tag == "td" and cls == "memItemRight":
            self._in_right = True

        # Brief description row
        elif tag == "td" and cls == "mdescRight":
            self._in_desc = True

        # Detailed doc block
        elif tag == "div" and cls == "memdoc":
            self._in_memdoc = True
            self._depth_memdoc = 1

        # Source file link at bottom
        elif tag == "li" and self._tag_stack.count("ul") and not self.source_file:
            pass  # handled via data

    def handle_endtag(self, tag):
        if self._skip_depth:
            self._skip_depth -= 1
            return

        if self._tag_stack:
            self._tag_stack.pop()

        if self._in_title and tag == "div":
            self.title = self._flush()
            self._in_title = False

        elif self._in_inherit and tag == "p":
            raw = self._flush()
            if "Inherits" in raw or "inherits" in raw:
                self.inherits = raw
            self._in_inherit = False

        elif self._in_heading and tag == "tr":
            if self._cur_section is not None:
                self._cur_section["heading"] = self._flush()
            self._in_heading = False

        elif self._in_left and tag == "td":
            if self._cur_member is not None:
                self._cur_member["type"] = self._flush()
            self._in_left = False

        elif self._in_right and tag == "td":
            if self._cur_member is not None:
                self._cur_member["sig"] = self._flush()
            self._in_right = False

        elif self._in_desc and tag == "td":
            desc = self._flush()
            # Apply to _cur_member if it already has a sig (hasn't been pushed yet),
            # otherwise fall back to the last pushed member in the current section.
            if self._cur_member and self._cur_member.get("sig"):
                self._cur_member["desc"] = desc
            elif self._cur_section and self._cur_section["members"]:
                self._cur_section["members"][-1]["desc"] = desc
            self._in_desc = False

        elif self._in_memdoc:
            if tag == "div":
                self._depth_memdoc -= 1
                if self._depth_memdoc == 0:
                    self._in_memdoc = False
                    self._flush()  # discard — detail text already in brief

    def handle_data(self, data):
        if self._skip_depth:
            return
        active = (self._in_title or self._in_heading or self._in_left or
                  self._in_right or self._in_desc or self._in_inherit or
                  self._in_memdoc)
        if active:
            self._buf += data

    def finalize(self):
        """Call after feed() to flush last section."""
        if self._cur_member and self._cur_section is not None:
            self._cur_section["members"].append(self._cur_member)
        if self._cur_section is not None:
            self.sections.append(self._cur_section)


# ------------------------------------------------------------------ rendering
def render_markdown(parser):
    lines = []
    lines.append(f"# {parser.title}\n")
    if parser.inherits:
        lines.append(f"_{parser.inherits}_\n")
    lines.append("")

    for section in parser.sections:
        heading = section["heading"].strip()
        members = [m for m in section["members"]
                   if m["sig"] or m["type"]]
        if not members:
            continue

        lines.append(f"## {heading}\n")

        for m in members:
            sig  = m["sig"].strip()
            typ  = m["type"].strip()
            desc = m["desc"].strip()

            # skip inherited-section headers (no real sig)
            if "inherited from" in sig.lower():
                continue
            if not sig:
                continue

            if typ:
                lines.append(f"- **`{typ} {sig}`**")
            else:
                lines.append(f"- **`{sig}`**")

            if desc:
                # strip trailing "More..." link text
                desc = re.sub(r'\s*More\.\.\.\s*$', '', desc).strip()
                if desc:
                    lines.append(f"  - {desc}")

        lines.append("")

    return "\n".join(lines)


# ------------------------------------------------------------------ main
def process_file(html_path, out_dir):
    with open(html_path, encoding="utf-8", errors="replace") as f:
        content = f.read()

    p = DoxygenParser()
    p.feed(content)
    p.finalize()

    if not p.title:
        return None

    md = render_markdown(p)

    # derive output filename from title, e.g. "Eleon.Modding.IPlayer Interface Reference" -> IPlayer.md
    name = p.title
    name = re.sub(r'\s+(Interface|Class|Struct)\s+Reference.*$', '', name, flags=re.I)
    name = name.split(".")[-1].strip()
    if not name:
        name = os.path.splitext(os.path.basename(html_path))[0]

    out_path = os.path.join(out_dir, name + ".md")
    with open(out_path, "w", encoding="utf-8") as f:
        f.write(md)

    return name


def main():
    os.makedirs(OUT_DIR, exist_ok=True)

    patterns = [
        lambda f: f.startswith("interface_") and not f.endswith("-members.html"),
        lambda f: f.startswith("class_")     and not f.endswith("-members.html"),
        lambda f: f.startswith("struct_")    and not f.endswith("-members.html"),
        lambda f: f.startswith("namespace_") and not f.endswith("-members.html"),
    ]

    files = sorted(os.listdir(SRC_DIR))
    processed = []

    for fname in files:
        if not fname.endswith(".html"):
            continue
        if any(p(fname) for p in patterns):
            full = os.path.join(SRC_DIR, fname)
            name = process_file(full, OUT_DIR)
            if name:
                processed.append(name)
                print(f"  {fname:70s} -> {name}.md")

    print(f"\nDone. {len(processed)} files written to {OUT_DIR}/")


if __name__ == "__main__":
    main()
