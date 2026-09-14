namespace DashSpec.Modeling.Parse

open DashSpec.Modeling.Parse.Lexing

module ParserUtilities =
    let createReader (text: string) = TokenReader(DashSpecLexer.tokenize text)