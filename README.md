This Visual Studio Extension provides basic support for discovering and running CucumberJS tests with Node.JS, in the Visual Studio Test Environment.

It borrows heavily from the Chutzpah test runner (https://github.com/mmanela/chutzpah), but is nowhere near as complete!

Getting Started
===============

1) Install node.js with NPM
2) Globally install cucumberjs using npm

Install globally with 

npm install -g cucumber

(Full instructions can be found here Cucumber can be found here: https://github.com/cucumber/cucumber-js)

Features
========

o Automatic discovery of newly added tests
o Automatic updating of modified tests
o Test timings

Known Issues
============

o It doesn't support traits
o Pretty printing isn't all that pretty
o It doesn't support debugging (anyone wants to help out with this, it'd be more than welcome)
o Discovering the node package install location would be better done by executing the NPM config command rather than building the expected default install location

