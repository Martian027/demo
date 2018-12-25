export class DateConverter {
    static dateToStr(date) {
        var d = date.getDate();
        var m = (date.getMonth() + 1);
        var y = date.getFullYear();
        return (d < 10 ? "0" : "") + d.toString() +
            (m < 10 ? "0" : "") + m.toString() +
            y.toString();
    }

    static parseDate(str) {
        if (!/^(\d){8}$/.test(str))
            return undefined;
        var y = Number.parseInt(str.substr(4, 4)),
            m = Number.parseInt(str.substr(2, 2)) - 1,
            d = Number.parseInt(str.substr(0, 2));
        var date = new Date(y, m, d);
        return date;
    }


    static dateToInputFormat(date) {
        var d = date.getDate();
        var m = (date.getMonth() + 1);
        var y = date.getFullYear();

        var str = y.toString() + "-" +
            (m < 10 ? "0" : "") + m.toString() + "-" +
            (d < 10 ? "0" : "") + d.toString();
        return str;
    }

    static isValidDate(d) {
        return d instanceof Date && !isNaN(d);
    }
}

