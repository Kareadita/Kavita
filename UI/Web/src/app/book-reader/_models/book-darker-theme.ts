// Important note about themes. Styles must be scoped to .book-content if not css variable overrides
export const BookDarkerTheme = `
:root .brtheme-darker {
  /* General */
  --color-scheme: dark;
  --bs-body-color: #919191;
  --hr-color: rgba(239, 239, 239, 0.125);
  --accent-bg-color: rgba(1, 4, 9, 0.5);
  --accent-text-color: #919191;
  --body-text-color: #919191;
  --btn-icon-filter: invert(1) grayscale(100%) brightness(200%);

  /* Drawer */
  --drawer-bg-color: #292929;
  --drawer-text-color: #919191;
  --drawer-pagination-horizontal-rule: inset 0 -1px 0 rgb(255 255 255 / 20%);
  --drawer-pagination-border: 1px solid rgb(0 0 0 / 13%);

  /* Accordion */
  --accordion-header-text-color: rgba(74, 198, 148, 0.7);
  --accordion-header-bg-color: rgba(52, 60, 70, 0.5);
  --accordion-body-bg-color: #292929;
  --accordion-body-border-color: rgba(239, 239, 239, 0.125);
  --accordion-body-text-color: var(--body-text-color);
  --accordion-header-collapsed-text-color: rgba(74, 198, 148, 0.7);
  --accordion-header-collapsed-bg-color: #292929;
  --accordion-button-focus-border-color: unset;
  --accordion-button-focus-box-shadow: unset;
  --accordion-active-body-bg-color: #292929;

  /* Buttons */
    --btn-focus-boxshadow-color: rgb(255 255 255 / 50%);
    --btn-primary-text-color: #ccc;
    --btn-primary-bg-color: var(--primary-color-dark-shade);
    --btn-primary-border-color: var(--primary-color-dark-shade);
    --btn-primary-hover-text-color: #ccc;
    --btn-primary-hover-bg-color: var(--primary-color-darker-shade);
    --btn-primary-hover-border-color: var(--primary-color-darker-shade);
    --btn-alt-bg-color: #424c72;
    --btn-alt-border-color: #444f75;
    --btn-alt-hover-bg-color: #3b4466;
    --btn-alt-focus-bg-color: #343c59;
    --btn-alt-focus-boxshadow-color: rgb(255 255 255 / 50%);
    --btn-fa-icon-color: #ccc;
    --btn-disabled-bg-color: #343a40;
    --btn-disabled-text-color: #ccc;
    --btn-disabled-border-color: #6c757d;

    /* Inputs */
    --input-bg-color: #292929;
    --input-bg-readonly-color: #434648;
    --input-focused-border-color: #ccc;
    --input-text-color: 919191;
    --input-placeholder-color: #919191;
    --input-border-color: #919191;
    --input-focus-boxshadow-color: rgb(255 255 255 / 10%);

    /* Nav (Tabs) */
    --nav-tab-border-color: rgba(44, 118, 88, 0.7);
    --nav-tab-text-color: #ccc;
    --nav-tab-bg-color: var(--primary-color-dark-shade);
    --nav-tab-hover-border-color: var(--primary-color-dark-shade);
    --nav-tab-active-text-color: #ccc;
    --nav-tab-border-hover-color: transparent;
    --nav-tab-hover-text-color: #ccc;
    --nav-tab-hover-bg-color: transparent;
    --nav-tab-border-top: rgba(44, 118, 88, 0.5);
    --nav-tab-border-left: rgba(44, 118, 88, 0.5);
    --nav-tab-border-bottom: rgba(44, 118, 88, 0.5);
    --nav-tab-border-right: rgba(44, 118, 88, 0.5);
    --nav-tab-hover-border-top: rgba(44, 118, 88, 0.5);
    --nav-tab-hover-border-left: rgba(44, 118, 88, 0.5);
    --nav-tab-hover-border-bottom: rgba(44, 118, 88, 0.5);
    --nav-tab-hover-border-right: rgba(44, 118, 88, 0.5);
    --nav-tab-active-hover-bg-color: var(--primary-color-dark-shade);
    --nav-link-bg-color: var(--primary-color-dark-shade);
    --nav-link-active-text-color: #ccc;
    --nav-link-text-color: #ccc;

    /* Checkboxes/Switch */
    --checkbox-checked-bg-color: var(--primary-color-dark-shade);
    --checkbox-border-color: var(--input-focused-border-color);
    --checkbox-focus-border-color: var(--primary-color-dark-shade);
    --checkbox-focus-boxshadow-color: rgb(255 255 255 / 50%);



    /* Reading Bar */
    --br-actionbar-button-text-color: #919191;
    --br-actionbar-button-hover-border-color: #6c757d;
    --br-actionbar-bg-color: black;
    
}



.book-content *:not(input), .book-content *:not(select), .book-content *:not(code), .book-content *:not(:link), .book-content *:not(.ngx-toastr) {
  color: #919191 !important;
}

.book-content code {
  color: #e83e8c !important;
}

.book-content :link, .book-content a {
  color: #8db2e5 !important;
}

.book-content img, .book-content img[src] {
z-index: 1;
filter: brightness(0.75) !important;
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
    color: #919191 !important;
}
  
.book-content :visited, .book-content :visited *, .book-content :visited *[class] {color: rgb(211, 138, 138) !important}
.book-content :link:not(cite), :link .book-content *:not(cite) {color: #8db2e5 !important}

`;