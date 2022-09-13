/// <reference types="Cypress" />

describe('First time server regestration', () => {

  const username = "Cypress"
  const email = "asdasdasdasd@easdasd.com"
  const password = "test123test123"

  it('access the server', () => {
    cy.visit('/')
    cy.contains('Kavita')
  })

  it('register', () => {
    cy.visit('/registration/register')

    cy.get('#username').type(username)
    cy.get('#email').type(email)
    cy.get('#password').type(password)
    cy.get('.btn').click()
  })

  it('log in', () => {
    cy.visit('/login')

    cy.get('#username').type(username)
    cy.get('#password').type(password)
    cy.get('.btn').click()
  })
})
