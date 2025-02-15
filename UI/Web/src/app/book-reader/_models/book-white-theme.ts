// Important note about themes. Must have one section with .reader-container that contains color, background-color and rest of the styles must be scoped to .book-content
export const BookWhiteTheme = `
  :root .brtheme-white {
    --drawer-text-color: white;
    --br-actionbar-bg-color: white;
    --bs-btn-active-color: black;
    --progress-bg-color: rgb(222, 226, 230);

    /* General */
    --color-scheme: light;
    --bs-body-color: black;
    --hr-color: rgba(239, 239, 239, 0.125);
    --accent-bg-color: rgba(1, 4, 9, 0.5);
    --accent-text-color: lightgrey;
    --body-text-color: black;
    --btn-icon-filter: invert(1) grayscale(100%) brightness(200%);

    /* Drawer */
    --drawer-bg-color: white;
    --drawer-text-color: black;
    --drawer-pagination-horizontal-rule: inset 0 -1px 0 rgb(255 255 255 / 20%);
    --drawer-pagination-border: 1px solid rgb(0 0 0 / 13%);


    /* Accordion */
    --accordion-header-text-color: rgba(74, 198, 148, 0.9);
    --accordion-header-bg-color: rgba(52, 60, 70, 0.5);
    --accordion-body-bg-color: white;
    --accordion-body-border-color: rgba(239, 239, 239, 0.125);
    --accordion-body-text-color: var(--body-text-color);
    --accordion-header-collapsed-text-color: rgba(74, 198, 148, 0.9);
    --accordion-header-collapsed-bg-color: white;
    --accordion-button-focus-border-color: unset;
    --accordion-button-focus-box-shadow: unset;
    --accordion-active-body-bg-color: white;

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
    --btn-fa-icon-color: black;
    --btn-disabled-bg-color: #343a40;
    --btn-disabled-text-color: #efefef;
    --btn-disabled-border-color: #6c757d;

    /* Inputs */
    --input-bg-color: white;
    --input-bg-readonly-color: white;
    --input-focused-border-color: #ccc;
    --input-text-color: black;
    --input-placeholder-color: black;
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
  --br-actionbar-button-text-color: black;
  --br-actionbar-button-hover-border-color: #6c757d;
  --br-actionbar-bg-color: white;

  /* Drawer */
  --drawer-pagination-horizontal-rule: inset 0 -1px 0 rgb(0 0 0 / 13%);
  --drawer-pagination-border: 1px solid rgb(0 0 0 / 13%);
}

.reader-container {
  color: black !important;
  background-image: none !important;
  background-color: white !important;
}


.book-content *:not(input), .book-content *:not(select), .book-content *:not(code), .book-content *:not(:link), .book-content *:not(.ngx-toastr) {
  color: black;
}

.book-content code {
  color: #e83e8c !important;
}

.book-content :link, .book-content a {
  color: #8db2e5 !important;
}

.book-content img, .book-content img[src] {
  z-index: 1;
  background-color: initial !important;
}


.book-content *:not(code), .book-content *:not(a) {
  background-color: white;
  box-shadow: none;
  text-shadow: none;
  border-radius: unset;
  color: #dcdcdc;
}

.book-content :visited, .book-content :visited *, .book-content :visited *[class] {
  color: rgb(240, 50, 50) !important;
}
.book-content :link:not(cite), :link .book-content *:not(cite) {
  color: #00f !important;
}

.btn-check:checked + .btn {
  color: white;
  background-color: var(--primary-color);
}

`;
