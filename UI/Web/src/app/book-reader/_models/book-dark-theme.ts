// Important note about themes. Must have one section with .reader-container that contains color, background-color and rest of the styles must be scoped to .book-content
export const BookDarkTheme = `
:root .brtheme-dark {
  --accordion-body-bg-color: black;
  --accordion-header-bg-color: grey;
  --br-actionbar-button-text-color: white;
  --br-actionbar-button-hover-border-color: #6c757d;
  --color-scheme: dark;

}

.reading-bar {
    --br-actionbar-button-text-color: white;
}

html, body { color-scheme: dark; }

.book-content *:not(input), .book-content *:not(select), .book-content *:not(code), .book-content *:not(:link), .book-content *:not(.ngx-toastr) {
  color: #dcdcdc !important;
}

.book-content code {
  color: #e83e8c !important;
}

.book-content :link, .book-content a {
  color: #8db2e5 !important;
}

.book-content img, .book-content img[src] {
z-index: 1;
filter: brightness(0.85) !important;
background-color: initial !important;
}

.reader-container {
  color: #dcdcdc !important;
  background-image: none !important;
  background-color: #292929 !important;
}

.book-content *:not(code), .book-content *:not(a) {
    background-color: #292929;
    box-shadow: none;
    text-shadow: none;
    border-radius: unset;
    color: #dcdcdc !important;
}
  
.book-content :visited, .book-content :visited *, .book-content :visited *[class] {color: rgb(211, 138, 138) !important}
.book-content :link:not(cite), :link .book-content *:not(cite) {color: #8db2e5 !important}
`;