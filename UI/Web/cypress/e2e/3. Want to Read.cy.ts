describe('User flows for Want to Read', () => {

  const username = "Cypress"
  const password = "test123test123"

  const bookname = 'Britannica' // Doesn't need to be the full title

  it('log in', () => {
    const username = "Cypress"
    const email = "asdasdasdasd@easdasd.com"
    const password = "test123test123"

    cy.visit('/login')
    cy.get('#username').type(username)
    cy.get('#password').type(password)
    cy.get('.btn').click()
  })

  function GoToWantToRead () {
    cy.get('[icon="fa-star"] > .side-nav-item').click()
  }

  function OpenSeriesPageActionMenu () {
    cy.get('.side-nav-item').contains('Books').click()
    cy.get('#jumpbar-index--0 > app-series-card > app-card-item > .card > .card-body').click()
    cy.get('.d-sm-block > .card-actions > app-card-actionables > .d-inline-block').click()
  }


  ///*
  //Add from Series Detail
  it('Add to Want to Read from series detail', () => {
    OpenSeriesPageActionMenu()
    cy.get('[data-popper-placement="bottom-start"] > .dropdown-menu > :nth-child(4)').click() // Add to Want to Read
    GoToWantToRead()
    cy.get('.card-body').contains(bookname)
  })

  //Remove from Series Detail
  it('Remove Want to Read from series detail', () => {
    OpenSeriesPageActionMenu()
    cy.get('[data-popper-placement="bottom-start"] > .dropdown-menu > :nth-child(5)').click() // Remove from Want to Read
    GoToWantToRead()
    cy.get('.main-container').contains("There are no items")
  })


  //Ability to add 1 series from library
  it('Add 1 Series from Library', () => {
    cy.get('.side-nav-item').contains('Books').click()

    cy.get('#jumpbar-index--0 > app-series-card > app-card-item > .card > .card-body').within(() => {
      cy.get('.fa').eq(1).click()
    })

    cy.get('[data-popper-placement="top-start"] > .dropdown-menu').contains('Add to Want').click()
    GoToWantToRead()
    cy.get('.card-body').contains(bookname)

  })


  //Ability to remove 1 series from library
  it('Remove 1 Series from Library', () => {
    cy.get('.side-nav-item').contains('Books').click()

    cy.get('#jumpbar-index--0 > app-series-card > app-card-item > .card > .card-body').within(() => {
      cy.get('.fa').eq(1).click()
    })

    cy.get('[data-popper-placement="top-start"] > .dropdown-menu').contains('Remove from Want').click()
    GoToWantToRead()
    cy.get('.main-container').contains("There are no items")

  })
  //*/
  /*
  //Ability to add muliple series from library
  it('Add muliple series from library', () => {
    cy.get('.side-nav-item').contains('Books').click()

    // The bulk selection checkbox is very elusive
    //cy.get('#jumpbar-index--1 > app-series-card.ng-star-inserted').invoke('show');

  })
  */

})

//Ability to add 1 series from library            - Done
//Ability to remove 1 series from library         - Done

//Ability to add muliple series from library      - WIP
//Ability to remove multiple series from library  -

//Add from Series Detail                          - Done
//Remove from Series Detail                       - Done

//Remove from Want To Read                        -
