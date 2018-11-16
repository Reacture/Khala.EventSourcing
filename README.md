# Khala - Event Sourcing

The implementation of Event Sourcing pattern for .NET Standard. Azure Table storage and relational databases are supported as event stores. This project consists of following packages.

|Package|Description|
|--|--|
|Khala.EventSourcing.Contracts|Define interfaces and base classes for domain events.|
|Khala.EventSourcing.Abstraction|Define interfaces for event store and provide a base class for event sourced objects.|
|Khala.EventSourcing.Azure|Provide the Azure Table storage based on implementation of event store and snapshot store.|
|Khala.EventSourcing.SqlCore|Provide the relational database based on implementation of event store.|

## Working Example

Refer to the project https://github.com/Reacture/FoxOffice

## License

```
MIT License

Copyright (c) 2017 Gyuwon Yi

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
