// Important note about themes. Must have one section with .reader-container that contains color, background-color and rest of the styles must be scoped to .book-content
export const BookBlackTheme = `
:root .brtheme-black {
  /* General */
  --color-scheme: dark;
  --bs-body-color: black;
  --hr-color: rgba(239, 239, 239, 0.125);
  --accent-bg-color: rgba(1, 4, 9, 0.5);
  --accent-text-color: lightgrey;
  --body-text-color: #efefef;
  --btn-icon-filter: invert(1) grayscale(100%) brightness(200%);

  /* Drawer */
  --drawer-bg-color: #292929;
  --drawer-text-color: white;
  --drawer-pagination-horizontal-rule: inset 0 -1px 0 rgb(255 255 255 / 20%);
  --drawer-pagination-border: 1px solid rgb(0 0 0 / 13%);
  

  /* Accordion */
  --accordion-header-text-color: rgba(74, 198, 148, 0.9);
  --accordion-header-bg-color: rgba(52, 60, 70, 0.5);
  --accordion-body-bg-color: #292929;
  --accordion-body-border-color: rgba(239, 239, 239, 0.125);
  --accordion-body-text-color: var(--body-text-color);
  --accordion-header-collapsed-text-color: rgba(74, 198, 148, 0.9);
  --accordion-header-collapsed-bg-color: #292929;
  --accordion-button-focus-border-color: unset;
  --accordion-button-focus-box-shadow: unset;
  --accordion-active-body-bg-color: #292929;

  /* Buttons */
    --btn-focus-boxshadow-color: rgb(255 255 255 / 50%);
    --btn-primary-text-color: white;
    --btn-primary-bg-color: var(--primary-color);
    --btn-primary-border-color: var(--primary-color);
    --btn-primary-hover-text-color: white;
    --btn-primary-hover-bg-color: var(--primary-color-darker-shade);
    --btn-primary-hover-border-color: var(--primary-color-darker-shade);
    --btn-alt-bg-color: #424c72;
    --btn-alt-border-color: #444f75;
    --btn-alt-hover-bg-color: #3b4466;
    --btn-alt-focus-bg-color: #343c59;
    --btn-alt-focus-boxshadow-color: rgb(255 255 255 / 50%);
    --btn-fa-icon-color: white;
    --btn-disabled-bg-color: #343a40;
    --btn-disabled-text-color: white;
    --btn-disabled-border-color: #6c757d;

    /* Inputs */
    --input-bg-color: #343a40;
    --input-bg-readonly-color: #434648;
    --input-focused-border-color: #ccc;
    --input-text-color: #fff;
    --input-placeholder-color: #aeaeae;
    --input-border-color: #ccc;
    --input-focus-boxshadow-color: rgb(255 255 255 / 50%);

    /* Nav (Tabs) */
    --nav-tab-border-color: rgba(44, 118, 88, 0.7);
    --nav-tab-text-color: var(--body-text-color);
    --nav-tab-bg-color: var(--primary-color);
    --nav-tab-hover-border-color: var(--primary-color);
    --nav-tab-active-text-color: white;
    --nav-tab-border-hover-color: transparent;
    --nav-tab-hover-text-color: var(--body-text-color);
    --nav-tab-hover-bg-color: transparent;
    --nav-tab-border-top: rgba(44, 118, 88, 0.7);
    --nav-tab-border-left: rgba(44, 118, 88, 0.7);
    --nav-tab-border-bottom: rgba(44, 118, 88, 0.7);
    --nav-tab-border-right: rgba(44, 118, 88, 0.7);
    --nav-tab-hover-border-top: rgba(44, 118, 88, 0.7);
    --nav-tab-hover-border-left: rgba(44, 118, 88, 0.7);
    --nav-tab-hover-border-bottom: var(--bs-body-bg);
    --nav-tab-hover-border-right: rgba(44, 118, 88, 0.7);
    --nav-tab-active-hover-bg-color: var(--primary-color);
    --nav-link-bg-color: var(--primary-color);
    --nav-link-active-text-color: white;
    --nav-link-text-color: white;



  /* Reading Bar */
  --br-actionbar-button-text-color: white;
  --br-actionbar-button-hover-border-color: #6c757d;
  --br-actionbar-bg-color: black;
}



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
  background-color: black !important;
}

.book-content *:not(code), .book-content *:not(a) {
    background-color: black;
    box-shadow: none;
    text-shadow: none;
    border-radius: unset;
    color: #dcdcdc !important;
}
  
.book-content :visited, .book-content :visited *, .book-content :visited *[class] {color: rgb(211, 138, 138) !important}
.book-content :link:not(cite), :link .book-content *:not(cite) {color: #8db2e5 !important}
`;