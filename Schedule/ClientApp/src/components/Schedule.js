import React, { Component } from 'react';
import { Link } from 'react-router-dom'
import { Redirect } from 'react-router-dom';
import { DateConverter } from './DateConverter';
export class Schedule extends Component {
    displayName = Schedule.name

    constructor(props) {
        super(props);
        var redirect;
        var date;
        if (props.match.params.dateStr == undefined) {
            date = new Date();
            redirect = true;
        }
        else {
            date = DateConverter.parseDate(props.match.params.dateStr);
            if (date == undefined) {
                date = new Date();
                redirect = true;
            }
            else {
                redirect = false;
            }
        }

        this.state = { date: date, scheduleRows: [], loading: true, redirect: redirect };
        if (!redirect) {
            this.loadSchedule();
            this.dateChanged.bind(this);
        }
    }

    loadSchedule() {

        this.setState({ loading: true });

        var str = DateConverter.dateToStr(this.state.date);

        fetch('api/CinemaProject/GetSchedule/' + str)
            .then(response => {
                if (response.ok) return response.json(); else throw response.text;
            })
            .then(data => {
                this.setState({ scheduleRows: data, loading: false });
            })
    }

    static renderSchedule(scheduleRows) {

        var timeStyle = {
            margin: '10px',
            float: 'left'
        };

        return (
            <table className="table" class="table-fixed-head">
                <thead>
                    <tr>
                        <th>Кинотеатр</th>
                        <th>Фильм</th>
                        <th>Время</th>
                        <th></th>
                    </tr>
                </thead>
                <tbody>
                    {scheduleRows.map(scheduleRow =>
                        <tr>
                            <td>{scheduleRow.movieTheaterName}</td>
                            <td>{scheduleRow.movieTitle}</td>
                            <td align="left">
                                {scheduleRow.startTimeList.map(time =>
                                    <div style={timeStyle}>{time.time.substring(0, 5)}</div>
                                )}
                            </td>
                            <td>
                                <Link to={"/Edit/" + scheduleRow.id}>Редактировать</Link>
                            </td>
                        </tr>
                    )}
                </tbody>
            </table>
        );
    }

    render() {

        if (this.state.redirect) {
            this.state.redirect = false;
            var path = this.state.path;
            if (path == undefined || path == "")
                path = "/schedule/" + DateConverter.dateToStr(this.state.date)
            return (<Redirect to={path} />);
        }

        var str;
        if (this.state.date == undefined) {
            str = "";
        }
        else {
            str = DateConverter.dateToInputFormat(this.state.date);
        }
        let selectDate = <p><em><input type="date" dateFormat="dd.MM.yyyy" defaultValue={str} required onChange={this.dateChanged.bind(this)} /></em>
            <button onClick={(e) => { e.preventDefault(); this.setNewDate(); return false; }}>Показать</button>
            <button onClick={(e) => { e.preventDefault(); this.addNew(); return false; }}>Добавить</button>
        </p>;
        let contents = (this.state.loading
            ? <p><em>Загрузка расписания...</em></p>
            : Schedule.renderSchedule(this.state.scheduleRows)
        );

        return (
            <div>
                <h1>Расписание фильмов</h1>
                <p></p>
                {selectDate}
                {contents}
            </div>
        );
    }
    dateChanged(e) {

        var newDate = new Date(e.target.value);
        if (DateConverter.isValidDate(newDate)) {
            this.setState({ date: newDate });
        }
    }



    setNewDate() {
        let path = "/schedule/" + DateConverter.dateToStr(this.state.date)
        this.setState({ redirect: true, path:path });
        this.loadSchedule();
    }

    addNew() {
        let path = "/new/" + DateConverter.dateToStr(this.state.date)
        this.props.history.push(path)
        this.setState({ redirect: true, path:path });
    }
}