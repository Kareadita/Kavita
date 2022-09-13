describe('Existing User.cy', () => {


  //TODO - Figure out paths that work for more than just my machine
  const librarypath = "F:\\Library\\.KavitaTesting\\WantToRead"
  const emptylibrarypath = "F:\\Library\\.KavitaTesting\\KeepEmpty"
  const libraryname = "Books"

  //beforeEach(()=>{
  //})


  it('log in', () => {
    const username = "Cypress"
    const email = "asdasdasdasd@easdasd.com"
    const password = "test123test123"

    cy.visit('/login')
    cy.get('#username').type(username)
    cy.get('#password').type(password)
    cy.get('.btn').click()
  })


  // Validate scan fails if library is an empty folder
  it('error message if a library is empty', () => {
    cy.get('.not-xs-only > .dark-exempt').click()
    cy.contains('Libraries').click()
    cy.contains('Add Library').click()

    cy.get('#library-name').type('Empty')
    cy.get('#library-type').select('Book')
    cy.get('.modal-body > h4 > .btn').click()

    cy.get('#typeahead-focus').type(emptylibrarypath) // This gives a harmless "Invalid Path" error
    cy.get('.component-host-scrollable > .modal-footer > .btn-primary').contains("Share").click({force: true})
    cy.get('.modal-footer > .btn-primary').contains("Save").click({force: true})

    // Wait 15 seconds to give the message time to appear
    cy.wait(15000)
    cy.get('app-nav-events-toggle > .btn').click()
    cy.contains("Some of the root folders for the library")
  })


  //Delete a library
  it('delete a library', () => {
    cy.get('.not-xs-only > .dark-exempt').click()
    cy.contains('Libraries').click()
    cy.get('.btn-danger').click()
    cy.get('.modal-footer').contains('Confirm').click()

    //Sometimes you need to click off and back it seems. Should probably be fixed
    cy.contains('Users').click()
    cy.contains('Libraries').click()

    // Validate the "no library" message shows
    cy.get('.list-group-item').contains('There are no libraries')
  })


  // Create a new library
  it('create a library', () => {

    cy.get('.not-xs-only > .dark-exempt').click()
    cy.contains('Libraries').click()
    cy.contains('Add Library').click()

    cy.get('#library-name').type('Books')
    cy.get('#library-type').select('Book')
    cy.get('.modal-body > h4 > .btn').click()

    cy.get('#typeahead-focus').type(librarypath) // This causes a "Invalid Path" Kavita error, but it's safe to ignore
    cy.get('.component-host-scrollable > .modal-footer > .btn-primary').contains("Share").click({force: true})
    cy.get('.modal-footer > .btn-primary').contains("Save").click({force: true})

    //TODO - should check for the library name on this page and the sidebar

  })


  // Validate that a new library scans
  it('Validate that a new library scans', () => {
    cy.wait(1000)
    cy.get('.ng-trigger').contains("A scan has")
    cy.wait(7000) // Seven seconds for 3 Books should be enough
    cy.get('.side-nav-item').contains('Books').click()
    cy.get('.subtitle-with-actionables').contains('3 Series')
  })


  //Scan a library
  it('Scan a library', () => {
    cy.get('.not-xs-only > .dark-exempt').click()
    cy.contains('Libraries').click()
    cy.get('h4 > .float-end > .btn-secondary').click()

    cy.wait(1000)
    cy.get('.ng-trigger').contains("A scan has")
  })


/*
  //Validate last scanned updates
  it('Validate last scanned library updates', () => {

  })

  //Edit library folders and validate scan happens
  it('Edit library folders and validate scan happens', () => {

  })

*/

})
