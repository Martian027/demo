import React, { Component } from 'react';
import { DateConverter } from './DateConverter';
export class Edit extends Component {
    constructor(props) {
        super(props);
        var id = this.props.match.params.id;
        var dateStr = this.props.match.params.dateStr;
        var date;
        if (dateStr == undefined) {
            date = new Date();
        }
        else {
            date = DateConverter.parseDate(dateStr);
            if (date == undefined) {
                date = new Date();
            }
        }

        if (id == undefined)
            id = 0;
        this.state = {
            loadingText: "Загрузка", loading: true, record: { id:0 }, movies: [], movieTheaters: [] };

        fetch('api/CinemaProject/GetMovies')
            .then(response => {
                if (response.ok) return response.json(); else throw response.text;
            })
            .then(movies => {
                fetch('api/CinemaProject/GetMovieTheaters')
                    .then(response => {
                        if (response.ok) return response.json(); else throw response.text;
                    })
                    .then(movieTheaters => {
                        this.setState({ movies: movies, movieTheaters: movieTheaters });
                        if (id == 0) {
                            var record = { id: 0, date: DateConverter.dateToInputFormat(date), movie: movies[0].id, movieTitle: movies[0].title, movieTheater: movieTheaters[0].id, movieTheaterName: movieTheaters[0].name, startTimeList: [] };
                            this.setState({record:record, loading: false });
                        }
                        else {
                            fetch('api/CinemaProject/GetScheduleByID/' + id.toString())
                                .then(response => {
                                    if (response.ok) return response.json(); else throw response.text;
                                })
                                .then(data => {
                                    this.setState({ record: data, loading: false });
                                });
                        }
                    });
            });

    }
    
    render() {
        
        var timeStyle = {
            margin: '1px 0px 1px 10px',
            float: 'left'
        };
      
        let deleteButton = this.state.record.id == 0 ?<br/>:<button onClick={(e) => { e.preventDefault(); this.delete(); return false; }}>Удалить</button>;


        let contents = this.state.loading ?
                <p>{this.state.loadingText}</p> : (<div>
                <form>
                    <fieldset>
                        <legend>{this.state.record.id == 0 ? "Новая запись" :"Редактирование"}</legend>
                        <label>Дата &nbsp;
                            <input type="date" dateFormat="dd.MM.yyyy" defaultValue={this.state.record.date.substring(0, 10)} onChange={(e) => this.setDate(e)}/>
                        </label>
                        <br />
                        <label>Кинотеар &nbsp;
                            <select onChange={(e) => { this.state.record.movieTheater = e.target.value; }}>
                                {this.state.movieTheaters.map(movieTheater => <option value={movieTheater.id} selected={movieTheater.id == this.state.record.movieTheater}>{movieTheater.name}</option>)}
                            </select>
                        </label>
                        <br />
                        <label>Фильм &nbsp;
                            <select onChange={(e) => { this.state.record.movie = e.target.value; }}>
                                {this.state.movies.map(movie => <option value={movie.id} selected={movie.id == this.state.record.movie}> {movie.title}</option>)}
                            </select>
                        </label>
                        <br />
                        <label>Сеансы &nbsp;
                            <div>
                                {this.state.record.startTimeList.map(time =>
                                    <div style={timeStyle}>
                                        <input type="time" defaultValue={time.time.substring(0, 5)} value={time.time.substring(0, 5)} onChange={(e) => { this.setTime(time, e.target.value); }} />
                                        <button onClick={(e) => { e.preventDefault(); this.setTime(time, null); return false; }}>X</button>
                                    </div>)
                                }

                                <div style={timeStyle}>
                                    <button onClick={(e) => { e.preventDefault(); this.addTime(); return false; }}>Добавить сеанс</button>
                                </div>
                            </div>
                        </label>
                        <br />

                    </fieldset>
                    <div>
                        <button onClick={(e) => { e.preventDefault(); this.save(); return false; }}>Сохранить</button>
                        <button onClick={(e) => { e.preventDefault(); this.cancel(); return false; }}>Отменить</button>
                        {deleteButton}
                    </div>
                </form>
            </div>)
        return <div>{contents}</div>;
    }

    addTime() {
        this.state.record.startTimeList.push({ id: 0, time: "00:00:00" });
        this.setState({ record: this.state.record, loading: false, movies: this.state.movies, movieTheaters: this.state.movieTheaters });
    }

    setTime(time, newValue) {
        if (newValue == "" || newValue==null) {
            //Если пустая строка, значит необходимо удалить сеанс
            this.state.record.startTimeList = this.state.record.startTimeList.filter(function (t) {
                return t !== time
            });
        }
        else {
            time.time = newValue;
        }
        this.setState({ record: this.state.record });
    }

    setDate(e) {
        var newDate = new Date(e.target.value);
        if (DateConverter.isValidDate(newDate))
            this.state.record.date = newDate;
    }

    save() {
        var isNew = this.state.record.id==0;
        var url = isNew ? "/api/CinemaProject" : "/api/CinemaProject/" + this.state.record.id;
        var method = isNew ? "post" : "put";
        var record = this.state.record;
        this.setState({ loading: true, loadingText: "Сохранение" });
        fetch(url,
            {
                method: method,
                headers: {
                    'Accept': 'application/json',
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(
                    record
                )
            }
        ).then(r => { if (r.ok) { this.setState({ loading: true, loadingText: "Изменения сохранены"}); this.redirect(); } });
    }

    delete() {
        this.setState({ loading: true, loadingText: "Удаление"});
        fetch("/api/CinemaProject/" + this.state.record.id,
            {
                method: "delete",
            }
        ).then(r => { if (r.ok) { this.setState({ loading: true, loadingText: "Запись удалена" }); this.redirect(); } });
    }


    cancel() { 
        this.redirect();
    }

    redirect() {
        this.props.history.goBack();
    }
}

