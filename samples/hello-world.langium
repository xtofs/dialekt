grammar HelloWorld

hidden terminal WS: /\s+/;
terminal ID: /[_a-zA-Z][\w]*/;

entry Model: (persons+=Person | greetings+=Greeting)*;

Person:
    'person' name=ID;

Greeting:
    'Hello' person=[Person] '!';
