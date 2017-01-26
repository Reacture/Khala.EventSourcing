# Reactive Essentials - Event Sourcing

이벤트 소싱(Event Sourcing) 패턴 구현체를 제공합니다.

- **이벤트 기반 집합체(aggregate)**
- **저장소(repository):** Azure Table storage, 관계형 데이터베이스
- **이벤트 발행(at-least-once delivery)**
- **스냅샷(memento pattern)**
- **유일성 제약 문자열 속성:** SQL 이벤트 저장소

## 패키지

### Core

이벤트 소싱 추상화 계층을 제공합니다.

```
> Install-Package ReactiveArchitecture.EventSourcing.Core
```

### Azure

NoSQL 키-값 저장소인 Azure Table storage를 사용하는 이벤트 저장소 구현체를 제공합니다.

```
> Install-Package ReactiveArchitecture.EventSourcing.Azure
```

### SQL

관계형 데이터베이스 대상 이벤트 저장소 구현체를 제공합니다.


```
> Install-Package ReactiveArchitecture.EventSourcing.Sql
```

## 메시징

이벤트 발행과 이벤트 직렬화를 위해 [ReactiveArchitecture.Messaging 프로젝트](https://github.com/ReactiveEssentials/ReactiveArchitecture.Messaging)를 사용합니다. 저장소 구현체는 `IMessageBus` 의존성을 요구합니다. `IMessageBus` 인터페이스 구현체는 개발 환경에 적합하게 직접 구현하거나 제공되는 구현체를 설치해 사용할 수 있습니다. 예를 들어 Owin 응용프로그램이 Azure Event Hubs를 사용해 이벤트 발행을 처리한다면 ReactiveArchitecture.Messaging.Azure.Owin 패키지를 설치합니다.

```
> Install-Package ReactiveArchitecture.Messaging.Azure.Owin
```

## 집합체(Aggregate)

[`EventSourced`](source/RA.EventSourcing/EventSourcing/EventSourced.cs) 클래스를 상속받아 이벤트 기반 집합체를 구현합니다.

```csharp
public class User : EventSourced
{
    public User(Guid id, string username)
        : base(id)
    {
        RaiseEvent(new UserCreated { Username = username });
    }

    private User(Guid id, IEnumerable<IDomainEvent> pastEvents)
        : base(id)
    {
        HandlePastEvents(pastEvents);
    }

    public static User Factory(Guid id, IEnumerable<IDomainEvent> pastEvents)
    {
        return new User(id, pastEvents);
    }

    public string Username { get; private set; }

    public void ChangeUsername(string username)
    {
        RaiseEvent(new UsernameChanged { Username = username });
    }

    // Auto-wired event-handlers

    private void Handle(UserCreated domainEvent)
    {
        Username = domainEvent.Username;
    }

    private void Handle(UsernameChanged domainEvent)
    {
        Username = domainEvent.Username;
    }
}

public class UserCreated : DomainEvent
{
    public string Username { get; set; }
}

public class UsernameChanged : DomainEvent
{
    public string Username { get; set; }
}
```

## 예제 응용프로그램

- [TodoList](examples/TodoList)

## License

```
MIT License

Copyright (c) 2017 Reactive Essentials

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
